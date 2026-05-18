import bpy
import math
import os
from mathutils import Vector

try:
    SCRIPT_DIR = os.path.dirname(__file__)
except NameError:
    SCRIPT_DIR = os.path.join(os.getcwd(), "Tools")

ROOT = os.path.abspath(os.path.join(SCRIPT_DIR, ".."))
BLEND_PATH = os.path.join(ROOT, "Assets", "Blender", "TechTrophy.blend")
FBX_PATH = os.path.join(ROOT, "Assets", "Resources", "TechTrophyBlender.fbx")

os.makedirs(os.path.dirname(BLEND_PATH), exist_ok=True)
os.makedirs(os.path.dirname(FBX_PATH), exist_ok=True)


def material(name, color, metallic=0.0, roughness=0.35, emission_strength=0.0):
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = color
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")

    if bsdf is not None:
        if "Base Color" in bsdf.inputs:
            bsdf.inputs["Base Color"].default_value = color
        if "Metallic" in bsdf.inputs:
            bsdf.inputs["Metallic"].default_value = metallic
        if "Roughness" in bsdf.inputs:
            bsdf.inputs["Roughness"].default_value = roughness
        if "Emission Color" in bsdf.inputs:
            bsdf.inputs["Emission Color"].default_value = color
        if "Emission Strength" in bsdf.inputs:
            bsdf.inputs["Emission Strength"].default_value = emission_strength

    return mat


def select_active(obj):
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


def shade_smooth(obj):
    select_active(obj)
    bpy.ops.object.shade_smooth()


def bevel(obj, amount=0.035, segments=2):
    modifier = obj.modifiers.new("soft bevel", "BEVEL")
    modifier.width = amount
    modifier.segments = segments
    modifier.affect = "EDGES"
    normal = obj.modifiers.new("weighted normals", "WEIGHTED_NORMAL")
    normal.keep_sharp = True
    return obj


def cube(name, location, dimensions, mat, rotation=(0, 0, 0), bevel_amount=0.035):
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=location, rotation=rotation)
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = dimensions
    obj.data.materials.append(mat)
    bevel(obj, bevel_amount)
    return obj


def cylinder(name, location, radius, depth, mat, vertices=48, rotation=(0, 0, 0)):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=depth, location=location, rotation=rotation)
    obj = bpy.context.object
    obj.name = name
    obj.data.materials.append(mat)
    shade_smooth(obj)
    return obj


def sphere(name, location, scale, mat, segments=24, rings=12):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments, ring_count=rings, radius=1.0, location=location)
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    obj.data.materials.append(mat)
    shade_smooth(obj)
    return obj


def cylinder_between(name, start, end, radius, mat, vertices=16):
    start_v = Vector(start)
    end_v = Vector(end)
    direction = end_v - start_v
    length = direction.length
    if length <= 0.0001:
        return None

    center = (start_v + end_v) * 0.5
    obj = cylinder(name, center, radius, length, mat, vertices=vertices)
    obj.rotation_euler = direction.to_track_quat("Z", "Y").to_euler()
    return obj


def text_mesh(name, text, location, rotation, size, mat):
    bpy.ops.object.text_add(location=location, rotation=rotation)
    obj = bpy.context.object
    obj.name = name
    obj.data.body = text
    obj.data.align_x = "CENTER"
    obj.data.align_y = "CENTER"
    obj.data.size = size
    obj.data.extrude = 0.012
    obj.data.resolution_u = 12
    obj.data.materials.append(mat)
    select_active(obj)
    bpy.ops.object.convert(target="MESH")
    return bpy.context.object


def hex_prism(name, radius, height, location, mat):
    verts = []
    faces = []
    for z in [-height * 0.5, height * 0.5]:
        for i in range(6):
            angle = math.tau * i / 6.0 + math.radians(30)
            verts.append((math.cos(angle) * radius, math.sin(angle) * radius, z))

    faces.append([0, 1, 2, 3, 4, 5])
    faces.append([11, 10, 9, 8, 7, 6])
    for i in range(6):
        faces.append([i, (i + 1) % 6, (i + 1) % 6 + 6, i + 6])

    mesh = bpy.data.meshes.new(name + "Mesh")
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    obj.location = location
    obj.data.materials.append(mat)
    bevel(obj, 0.02, 1)
    return obj


bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete()

black = material("TechTrophy_Polished_Black", (0.015, 0.017, 0.02, 1.0), 0.65, 0.18)
matte_black = material("TechTrophy_Matte_Black", (0.035, 0.04, 0.045, 1.0), 0.35, 0.46)
gold = material("TechTrophy_Warm_Gold", (1.0, 0.66, 0.18, 1.0), 0.88, 0.2)
silver = material("TechTrophy_Brushed_Silver", (0.72, 0.74, 0.72, 1.0), 0.78, 0.24)
blue = material("TechTrophy_Deep_Blue_Glass", (0.035, 0.16, 0.42, 1.0), 0.25, 0.12)
cyan = material("TechTrophy_Cyan_Glow", (0.0, 0.95, 1.0, 1.0), 0.0, 0.06, 1.2)
green = material("TechTrophy_Green_LED", (0.05, 1.0, 0.35, 1.0), 0.0, 0.08, 1.0)
white = material("TechTrophy_White_Mark", (0.92, 0.92, 0.86, 1.0), 0.0, 0.32)

objects = []

# Compact premium base.
base = cylinder("TechTrophy_Black_Hex_Base", (0, 0, -0.76), 0.95, 0.22, black, vertices=6)
base.rotation_euler[2] = math.radians(30)
base.scale.x = 1.12
base.scale.y = 0.92
objects.append(base)

rim = cylinder("TechTrophy_Gold_Base_Rim", (0, 0, -0.60), 0.78, 0.08, gold, vertices=6)
rim.rotation_euler[2] = math.radians(30)
rim.scale.x = 1.06
rim.scale.y = 0.88
objects.append(rim)

plaque = cube("TechTrophy_Front_Plaque", (0, -0.72, -0.52), (0.78, 0.055, 0.20), gold, rotation=(math.radians(8), 0, 0), bevel_amount=0.025)
objects.append(plaque)
objects.append(text_mesh("TechTrophy_Plaque_Text", "ИИР НГУ", (0, -0.756, -0.492), (math.radians(82), 0, 0), 0.13, black))

# Processor pedestal and chip.
chip = cube("StudentMade_Tech_Trophy_AI_Chip", (0, 0, -0.28), (0.92, 0.92, 0.11), matte_black, bevel_amount=0.045)
objects.append(chip)

core_socket = cube("TechTrophy_Gold_Core_Socket", (0, 0, -0.18), (0.43, 0.43, 0.07), gold, rotation=(0, 0, math.radians(45)), bevel_amount=0.03)
objects.append(core_socket)

for i in range(24):
    angle = math.tau * i / 24.0
    outer = Vector((math.cos(angle) * 0.63, math.sin(angle) * 0.63, -0.20))
    inner = Vector((math.cos(angle) * 0.48, math.sin(angle) * 0.48, -0.20))
    pin = cylinder_between("TechTrophy_Chip_Pin_%02d" % (i + 1), outer, inner, 0.011, gold, vertices=8)
    objects.append(pin)

for i in range(8):
    angle = math.tau * i / 8.0
    start = (math.cos(angle) * 0.25, math.sin(angle) * 0.25, -0.105)
    end = (math.cos(angle) * 0.78, math.sin(angle) * 0.78, -0.105)
    trace = cylinder_between("TechTrophy_Circuit_Trace_%02d" % (i + 1), start, end, 0.015, cyan if i % 2 else gold, vertices=8)
    objects.append(trace)

for x, y in [(-0.58, -0.58), (0.58, -0.58), (-0.58, 0.58), (0.58, 0.58)]:
    objects.append(sphere("TechTrophy_Corner_LED", (x, y, -0.10), (0.045, 0.045, 0.045), green, 16, 8))

# Floating technological core.
core = hex_prism("TechTrophy_Floating_Hologram_Core", 0.24, 0.78, (0, 0, 0.48), blue)
objects.append(core)
inner_core = hex_prism("TechTrophy_Inner_Cyan_Core", 0.12, 0.55, (0, 0, 0.48), cyan)
objects.append(inner_core)

for name, z, radius, rot in [
    ("TechTrophy_Cyan_Orbit_Ring_Low", 0.30, 0.48, (math.radians(90), 0, math.radians(18))),
    ("TechTrophy_Gold_Orbit_Ring_Mid", 0.50, 0.55, (math.radians(67), math.radians(22), math.radians(-36))),
    ("TechTrophy_Cyan_Orbit_Ring_High", 0.69, 0.43, (math.radians(112), math.radians(-20), math.radians(42))),
]:
    bpy.ops.mesh.primitive_torus_add(major_radius=radius, minor_radius=0.015, major_segments=64, minor_segments=6, location=(0, 0, z), rotation=rot)
    ring = bpy.context.object
    ring.name = name
    ring.data.materials.append(cyan if "Cyan" in name else gold)
    shade_smooth(ring)
    objects.append(ring)

# Vertical support arcs/posts.
for angle in [math.radians(45), math.radians(135), math.radians(225), math.radians(315)]:
    x = math.cos(angle) * 0.44
    y = math.sin(angle) * 0.44
    objects.append(cylinder_between("TechTrophy_Silver_Core_Post", (x, y, -0.12), (x * 0.52, y * 0.52, 0.23), 0.022, silver, vertices=12))
    objects.append(sphere("TechTrophy_Post_Glow_Node", (x * 0.52, y * 0.52, 0.24), (0.035, 0.035, 0.035), cyan, 16, 8))

# Small title mark on chip top, simple and not visually noisy.
objects.append(text_mesh("TechTrophy_AI_Mark", "AI", (0, 0.02, 0.92), (0, 0, 0), 0.17, white))

# Lights/camera for inspection.
bpy.ops.object.light_add(type="AREA", location=(-2.4, -3.0, 3.3), rotation=(math.radians(62), 0, math.radians(-35)))
key = bpy.context.object
key.name = "TechTrophy_Preview_Key_Light"
key.data.energy = 520
key.data.size = 4.0

bpy.ops.object.light_add(type="POINT", location=(1.8, 1.6, 1.5))
rim_light = bpy.context.object
rim_light.name = "TechTrophy_Preview_Rim_Light"
rim_light.data.energy = 180

bpy.ops.object.camera_add(location=(1.9, -2.4, 1.45), rotation=(math.radians(64), 0, math.radians(38)))
bpy.context.scene.camera = bpy.context.object

bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)

bpy.ops.object.select_all(action="DESELECT")
for obj in objects:
    if obj is not None and obj.type == "MESH":
        obj.select_set(True)

bpy.context.view_layer.objects.active = chip
bpy.ops.export_scene.fbx(
    filepath=FBX_PATH,
    use_selection=True,
    object_types={"MESH"},
    apply_unit_scale=True,
    bake_space_transform=False,
)
