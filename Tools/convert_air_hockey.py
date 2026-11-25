"""Convert the supplied static GLBs to an editor-only interchange (no runtime plugin).

Usage: python Tools/convert_air_hockey.py --source <folder with both GLBs>
Only uncompressed, static triangle meshes are supported; unsupported data fails early.
Original GLBs and their embedded attribution are retained in SourceAssets/AirHockey.
"""
import argparse
import json
import shutil
import struct
from pathlib import Path

import numpy as np
from PIL import Image


def convert(source, output):
    raw = source.read_bytes()
    magic, version, length = struct.unpack_from('<III', raw)
    assert magic == 0x46546C67 and version == 2 and length == len(raw)
    json_size, json_type = struct.unpack_from('<II', raw, 12)
    assert json_type == 0x4E4F534A
    doc = json.loads(raw[20:20 + json_size])
    bin_size, bin_type = struct.unpack_from('<II', raw, 20 + json_size)
    assert bin_type == 0x004E4942
    blob = raw[28 + json_size:28 + json_size + bin_size]
    assert not doc.get('skins') and not doc.get('animations')
    assert not doc.get('extensionsRequired'), 'Compressed/extended GLB needs a full glTF importer.'
    stem = source.stem
    output.mkdir(parents=True, exist_ok=True)

    def accessor(index):
        a = doc['accessors'][index]
        assert 'sparse' not in a and not a.get('normalized')
        view = doc['bufferViews'][a['bufferView']]
        dtype = np.dtype({5121: 'u1', 5123: '<u2', 5125: '<u4', 5126: '<f4'}[a['componentType']])
        width = {'SCALAR': 1, 'VEC2': 2, 'VEC3': 3, 'VEC4': 4}[a['type']]
        offset = view.get('byteOffset', 0) + a.get('byteOffset', 0)
        return np.ndarray((a['count'], width), dtype=dtype, buffer=blob, offset=offset,
                          strides=(view.get('byteStride', dtype.itemsize * width), dtype.itemsize)).copy()

    images = []
    for i, img in enumerate(doc.get('images', [])):
        view = doc['bufferViews'][img['bufferView']]
        name = f"{stem}_{i}.{'png' if img['mimeType'] == 'image/png' else 'jpg'}"
        start = view.get('byteOffset', 0)
        (output / name).write_bytes(blob[start:start + view['byteLength']])
        images.append(name)

    def texture(info):
        return images[doc['textures'][info['index']]['source']] if info else ''

    mats = []
    for mat in doc.get('materials', []):
        pbr = mat.get('pbrMetallicRoughness', {})
        metallic_texture = texture(pbr.get('metallicRoughnessTexture'))
        if metallic_texture:
            packed_name = f'{stem}_metallic_smoothness.png'
            packed = np.asarray(Image.open(output / metallic_texture).convert('RGBA')).copy()
            packed[:, :, 0] = packed[:, :, 2]
            packed[:, :, 3] = 255 - packed[:, :, 1]
            packed[:, :, 1:3] = 0
            Image.fromarray(packed).save(output / packed_name)
            metallic_texture = packed_name
        mats.append(dict(name=mat.get('name', 'Material'), color=pbr.get('baseColorFactor', [1, 1, 1, 1]),
                         metallic=pbr.get('metallicFactor', 1), roughness=pbr.get('roughnessFactor', 1),
                         albedo=texture(pbr.get('baseColorTexture')), normal=texture(mat.get('normalTexture')),
                         emission=texture(mat.get('emissiveTexture')),
                         emissionColor=mat.get('emissiveFactor', [0, 0, 0]),
                         metallicRoughness=metallic_texture))
    meshes = []
    def vectors(values, keys):
        return [dict(zip(keys, [round(float(v), 7) for v in row])) for row in values]

    def visit(index, parent, path):
        node = doc['nodes'][index]
        assert 'skin' not in node
        local = np.eye(4)
        if 'matrix' in node:
            local = np.array(node['matrix']).reshape((4, 4), order='F')
        else:
            x, y, z, w = node.get('rotation', [0, 0, 0, 1])
            local[:3, :3] = [[1-2*(y*y+z*z), 2*(x*y-z*w), 2*(x*z+y*w)],
                              [2*(x*y+z*w), 1-2*(x*x+z*z), 2*(y*z-x*w)],
                              [2*(x*z-y*w), 2*(y*z+x*w), 1-2*(x*x+y*y)]]
            local[:3, :3] = local[:3, :3] @ np.diag(node.get('scale', [1, 1, 1]))
            local[:3, 3] = node.get('translation', [0, 0, 0])
        world = parent @ local
        path = path + '/' + node.get('name', str(index))
        for p in doc.get('meshes', [])[node['mesh']]['primitives'] if 'mesh' in node else []:
            assert p.get('mode', 4) == 4 and 'targets' not in p
            # glTF RH -> Unity LH: mirror X and reverse triangle winding.
            matrix = np.diag([-1, 1, 1, 1]) @ world
            pos = accessor(p['attributes']['POSITION'])
            pos = pos @ matrix[:3, :3].T + matrix[:3, 3]
            normals = accessor(p['attributes']['NORMAL']) @ np.linalg.inv(matrix[:3, :3])
            normals /= np.linalg.norm(normals, axis=1)[:, None]
            uv = accessor(p['attributes']['TEXCOORD_0'])
            uv[:, 1] = 1 - uv[:, 1]
            indices = accessor(p['indices']).reshape(-1, 3)
            if np.linalg.det(matrix[:3, :3]) < 0:
                indices = indices[:, [0, 2, 1]]
            meshes.append(dict(name=path, material=p.get('material', 0), vertices=vectors(pos, 'xyz'),
                               normals=vectors(normals, 'xyz'), uv=vectors(uv, 'xy'),
                               triangles=indices.flatten().tolist()))
            if stem == 'air_hockey':
                # The source is a combined mesh. Exclude only its three detached,
                # tabletop props from the playable variant; retain the original.
                unique, inverse = np.unique(pos.round(5), axis=0, return_inverse=True)
                parents = np.arange(len(unique))
                def root(a):
                    while parents[a] != a:
                        parents[a] = parents[parents[a]]
                        a = parents[a]
                    return a
                for tri in inverse[indices]:
                    a = root(tri[0])
                    for b in tri[1:]:
                        parents[root(b)] = a
                groups = {}
                for face_index, tri in enumerate(inverse[indices]):
                    groups.setdefault(root(tri[0]), []).append(face_index)
                keep = np.ones(len(indices), dtype=bool)
                removed = 0
                for faces_in_group in groups.values():
                    points = pos[indices[faces_in_group]].reshape(-1, 3)
                    low, high = points.min(0), points.max(0)
                    if (low[0] > -1 and high[0] < 1 and low[1] > 2.68 and high[1] < 3.1
                            and (low[2] > 2.8 or high[2] < -2.8)):
                        keep[faces_in_group] = False
                        removed += 1
                assert removed == 3, f'Expected exactly three decorative props, found {removed}'
                meshes[-1]['playableTriangles'] = indices[keep].flatten().tolist()
            faces = pos[indices]
            areas = np.linalg.norm(np.cross(faces[:, 1]-faces[:, 0], faces[:, 2]-faces[:, 0]), axis=1) / 2
            flat = np.ptp(faces[:, :, 1], axis=1) < 0.001
            levels = {}
            for height, area in zip(np.round(faces[flat, :, 1].mean(axis=1), 3), areas[flat]):
                levels[float(height)] = levels.get(float(height), 0) + float(area)
            print(stem, path, 'bounds', pos.min(axis=0).round(3), pos.max(axis=0).round(3),
                  'horizontal areas', sorted(levels.items(), key=lambda a: -a[1])[:6])
        for child in node.get('children', []):
            visit(child, world, path)

    for root in doc['scenes'][doc.get('scene', 0)]['nodes']:
        visit(root, np.eye(4), '')
    result = dict(name=stem, materials=mats, meshes=meshes, attribution=doc['asset'].get('extras', {}))
    (output / f'{stem}.meshdata.json').write_text(json.dumps(result, separators=(',', ':')), encoding='utf-8')
    return doc['asset'].get('extras', {})


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--source', type=Path, required=True)
    args = parser.parse_args()
    project = Path(__file__).resolve().parents[1]
    originals = project / 'SourceAssets/AirHockey'
    originals.mkdir(parents=True, exist_ok=True)
    for stem in ['air_hockey', 'air_hockey_cabinet']:
        source = args.source / f'{stem}.glb'
        if source.resolve() != (originals / source.name).resolve():
            shutil.copy2(source, originals / source.name)
        convert(source, project / 'Assets/Art/AirHockey/Interchange')
