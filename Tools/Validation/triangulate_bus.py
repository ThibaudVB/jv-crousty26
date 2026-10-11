"""Run with Blender --background --python this_file -- source.fbx output.fbx.
Triangulate the Crousty body while preserving FBX IDs, transforms and corner data.
"""
import sys
from array import array
import bpy
from io_scene_fbx import parse_fbx, encode_bin
source, destination = sys.argv[sys.argv.index('--') + 1:]
root, version = parse_fbx.parse(source)
def child(node, name):
    return next(e for e in node.elems if e.id == name)
geometry = next(g for g in child(root, b'Objects').elems
                if g.id == b'Geometry' and g.props[0] == 10049385)
coords = child(geometry, b'Vertices').props[0]
indices = child(geometry, b'PolygonVertexIndex').props[0]
polygons, face = [], []
for index in indices:
    face.append(index if index >= 0 else -index - 1)
    if index < 0:
        polygons.append(face)
        face = []
assert not face
mesh = bpy.data.meshes.new('Crousty triangulation')
mesh.from_pydata([coords[i:i+3] for i in range(0, len(coords), 3)], [], polygons)
mesh.calc_loop_triangles()
loops = [loop for triangle in mesh.loop_triangles for loop in triangle.loops]
parents = [triangle.polygon_index for triangle in mesh.loop_triangles]
new_indices = []
for triangle in mesh.loop_triangles:
    a, b, c = triangle.vertices
    new_indices.extend((a, b, -c-1))
child(geometry, b'PolygonVertexIndex').props[0] = array('i', new_indices)
for layer in geometry.elems:
    if not layer.id.startswith(b'LayerElement'):
        continue
    mapping = child(layer, b'MappingInformationType').props[0]
    reference = child(layer, b'ReferenceInformationType').props[0]
    assert reference == b'IndexToDirect', (layer.id, reference)
    if mapping == b'ByPolygonVertex':
        index_node = next(e for e in layer.elems if e.id.endswith(b'Index'))
        values = index_node.props[0]
        assert len(values) == len(indices)
        index_node.props[0] = array(values.typecode, (values[i] for i in loops))
    elif mapping == b'ByPolygon':
        assert layer.id == b'LayerElementMaterial'
        node = child(layer, b'Materials')
        values = node.props[0]
        node.props[0] = array(values.typecode, (values[i] for i in parents))
    else:
        assert mapping in (b'AllSame', b'ByVertice', b'ByVertex'), mapping
# FBX edges reference the first polygon corner representing each unique edge.
edges, seen = [], set()
for i in range(0, len(new_indices), 3):
    verts = [j if j >= 0 else -j-1 for j in new_indices[i:i+3]]
    for j in range(3):
        edge = tuple(sorted((verts[j], verts[(j+1)%3])))
        if edge not in seen:
            seen.add(edge)
            edges.append(i+j)
child(geometry, b'Edges').props[0] = array('i', edges)
methods = {'Y':'add_int16', 'C':'add_char', 'B':'add_bool', 'Z':'add_int8', 'I':'add_int32', 'L':'add_int64',
           'F':'add_float32', 'D':'add_float64', 'R':'add_bytes', 'S':'add_string',
           'f':'add_float32_array', 'd':'add_float64_array', 'i':'add_int32_array',
           'l':'add_int64_array', 'b':'add_bool_array', 'c':'add_byte_array'}
def encode(node):
    result = encode_bin.FBXElem(node.id)
    for kind, value in zip(node.props_type, node.props):
        getattr(result, methods[chr(kind)])(value)
    result.elems = [encode(e) for e in node.elems]
    return result
encode_bin.write(destination, encode(root), version)
print('TRIANGULATED', len(polygons), 'polygons ->', len(parents), 'triangles; IDs and corner attributes preserved')
