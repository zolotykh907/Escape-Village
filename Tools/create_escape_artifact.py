import bpy
import math
import os
from mathutils import Vector

try:
    SCRIPT_DIR = os.path.dirname(__file__)
except NameError:
    SCRIPT_DIR = os.path.join(os.getcwd(), "Tools")

ROOT = os.path.abspath(os.path.join(SCRIPT_DIR, ".."))
BLEND_PATH = os.path.join(ROOT, "Assets", "Blender", "EscapeArtifact.blend")
FBX_PATH = os.path.join(ROOT, "Assets", "Resources", "EscapeArtifactBlender.fbx")

os.makedirs(os.path.dirname(BLEND_PATH), exist_ok=True)
os.makedirs(os.path.dirname(FBX_PATH), exist_ok=True)


def material(name, color, metallic=0.0, roughness=0.35):
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
        if "Emission Strength" in bsdf.inputs and "Glow" in name:
            bsdf.inputs["Emission Strength"].default_value = 0.9

    return mat


def select_active(obj):
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


def shade_smooth(obj):
    select_active(obj)
    bpy.ops.object.shade_smooth()


def add_bevel(obj, amount=0.035, segments=2):
    bevel = obj.modifiers.new("soft bevel", "BEVEL")
    bevel.width = amount
    bevel.segments = segments
    bevel.affect = "EDGES"

    normal = obj.modifiers.new("weighted normals", "WEIGHTED_NORMAL")
    normal.keep_sharp = True
    return obj


def cube(name, location, dimensions, mat, rotation=(0, 0, 0), bevel=0.035):
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=location, rotation=rotation)
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = dimensions
    obj.data.materials.append(mat)
    add_bevel(obj, bevel)
    return obj


def cylinder(name, location, radius, depth, mat, vertices=32, rotation=(0, 0, 0), smooth=True):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=depth, location=location, rotation=rotation)
    obj = bpy.context.object
    obj.name = name
    obj.data.materials.append(mat)
    if smooth:
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


def cylinder_between(name, start, end, radius, mat, vertices=18):
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


def add_text_mesh(name, text, location, rotation, size, mat, align="CENTER"):
    bpy.ops.object.text_add(location=location, rotation=rotation)
    obj = bpy.context.object
    obj.name = name
    obj.data.body = text
    obj.data.align_x = align
    obj.data.align_y = "CENTER"
    obj.data.size = size
    obj.data.extrude = 0.012
    obj.data.resolution_u = 12
    obj.data.materials.append(mat)
    select_active(obj)
    bpy.ops.object.convert(target="MESH")
    return bpy.context.object


def add_rivet_row(prefix, x_values, y, z, mat, radius=0.025):
    objects = []
    for index, x in enumerate(x_values):
        objects.append(sphere("%s_%02d" % (prefix, index + 1), (x, y, z), (radius, radius, radius), mat, 12, 6))
    return objects


bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete()

silver = material("RoboDog_Brushed_Silver", (0.64, 0.62, 0.54, 1.0), 0.75, 0.24)
dark_silver = material("RoboDog_Gunmetal", (0.22, 0.24, 0.25, 1.0), 0.8, 0.28)
black = material("RoboDog_Black_Rubber", (0.015, 0.016, 0.014, 1.0), 0.25, 0.42)
deep_black = material("RoboDog_Matte_Black", (0.005, 0.006, 0.007, 1.0), 0.55, 0.5)
blue = material("RoboDog_Blue_Body_Panel", (0.08, 0.18, 0.34, 1.0), 0.75, 0.22)
white = material("RoboDog_White_Label", (0.92, 0.92, 0.86, 1.0), 0.0, 0.25)
glow = material("RoboDog_Glow_Cyan", (0.1, 0.95, 0.78, 1.0), 0.0, 0.06)
green_glow = material("RoboDog_Glow_Green", (0.05, 1.0, 0.25, 1.0), 0.0, 0.06)
gold = material("Trophy_Warm_Gold", (1.0, 0.68, 0.18, 1.0), 0.9, 0.18)
base_black = material("Trophy_Polished_Black_Base", (0.025, 0.022, 0.02, 1.0), 0.55, 0.2)

mesh_objects = []

# Trophy base.
base = cylinder("RoboDog_Trophy_Oval_Base", (0, 0, -0.72), 1.0, 0.22, base_black, vertices=48)
base.scale.x = 1.55
base.scale.y = 0.78
mesh_objects.append(base)

gold_rim = cylinder("RoboDog_Trophy_Gold_Rim", (0, 0, -0.57), 1.0, 0.07, gold, vertices=48)
gold_rim.scale.x = 1.36
gold_rim.scale.y = 0.64
mesh_objects.append(gold_rim)

plaque = cube("RoboDog_Trophy_Nameplate", (-0.05, -0.68, -0.50), (0.9, 0.055, 0.22), gold, rotation=(math.radians(8), 0, 0), bevel=0.025)
mesh_objects.append(plaque)
mesh_objects.append(add_text_mesh("RoboDog_Trophy_Label", "ИИР НГУ", (-0.05, -0.715, -0.47), (math.radians(82), 0, 0), 0.15, deep_black))

# Main robotic dog body.
body = cube("StudentMade_RoboDog_Long_Body", (0.16, 0, 0.84), (1.76, 0.52, 0.42), blue, bevel=0.08)
mesh_objects.append(body)

top_panel = cube("RoboDog_Black_Top_Rail", (0.08, 0, 1.085), (1.44, 0.34, 0.05), deep_black, bevel=0.025)
mesh_objects.append(top_panel)
mesh_objects.append(cube("RoboDog_Back_Service_Panel", (0.55, -0.272, 0.89), (0.64, 0.025, 0.24), dark_silver, bevel=0.018))
mesh_objects.append(cube("RoboDog_Side_Black_Panel_L", (-0.02, -0.284, 0.80), (0.34, 0.03, 0.16), deep_black, bevel=0.018))
mesh_objects.append(cube("RoboDog_Side_Black_Panel_R", (-0.02, 0.284, 0.80), (0.34, 0.03, 0.16), deep_black, bevel=0.018))

for obj in add_rivet_row("RoboDog_Left_Side_Rivet", [-0.42, -0.25, 0.31, 0.48], -0.306, 1.05, black, 0.023):
    mesh_objects.append(obj)
for obj in add_rivet_row("RoboDog_Right_Side_Rivet", [-0.42, -0.25, 0.31, 0.48], 0.306, 1.05, black, 0.023):
    mesh_objects.append(obj)

# Head and face cameras.
neck = cylinder_between("RoboDog_Neck_Actuator", (-0.74, 0, 0.88), (-0.98, 0, 0.92), 0.105, dark_silver, vertices=24)
mesh_objects.append(neck)

head = cube("RoboDog_Faceted_Head", (-1.24, 0, 0.93), (0.55, 0.48, 0.42), silver, bevel=0.075)
mesh_objects.append(head)
muzzle = cube("RoboDog_Sensor_Muzzle", (-1.52, 0, 0.86), (0.16, 0.34, 0.22), silver, bevel=0.055)
mesh_objects.append(muzzle)
mesh_objects.append(cube("RoboDog_Forehead_Lidar_Bump", (-1.30, 0, 1.18), (0.22, 0.24, 0.075), silver, bevel=0.035))

for y in [-0.11, 0.11]:
    lens_ring = cylinder("RoboDog_Face_Camera_Ring_%s" % ("L" if y < 0 else "R"), (-1.615, y, 1.02), 0.067, 0.025, black, vertices=28, rotation=(0, math.radians(90), 0))
    lens = cylinder("RoboDog_Face_Camera_Glass_%s" % ("L" if y < 0 else "R"), (-1.632, y, 1.02), 0.041, 0.018, glow, vertices=24, rotation=(0, math.radians(90), 0))
    mesh_objects.extend([lens_ring, lens])

for y, z in [(-0.095, 0.80), (0.095, 0.80), (-0.095, 0.70), (0.095, 0.70)]:
    sensor = cylinder("RoboDog_Lower_Sensor", (-1.625, y, z), 0.043, 0.018, black, vertices=20, rotation=(0, math.radians(90), 0))
    mesh_objects.append(sensor)

mesh_objects.append(sphere("RoboDog_Green_Status_LED", (-1.47, -0.265, 0.94), (0.035, 0.035, 0.035), green_glow, 16, 8))

# Top cable/handle.
handle_front = cylinder_between("RoboDog_Top_Handle_Front", (-0.52, -0.13, 1.17), (0.50, -0.13, 1.17), 0.025, deep_black, vertices=12)
handle_back = cylinder_between("RoboDog_Top_Handle_Back", (-0.52, 0.13, 1.17), (0.50, 0.13, 1.17), 0.025, deep_black, vertices=12)
for obj in [handle_front, handle_back]:
    mesh_objects.append(obj)
for x in [-0.52, 0.50]:
    mesh_objects.append(cylinder_between("RoboDog_Handle_Crossbar", (x, -0.13, 1.17), (x, 0.13, 1.17), 0.025, deep_black, vertices=12))
    mesh_objects.append(cube("RoboDog_Handle_Mount", (x, 0, 1.105), (0.12, 0.26, 0.055), dark_silver, bevel=0.02))

# Legs, joints and paws.
leg_specs = [
    ("FrontLeft", -0.58, -0.33, -0.92, -0.47),
    ("FrontRight", -0.58, 0.33, -0.92, 0.47),
    ("RearLeft", 0.70, -0.33, 1.02, -0.47),
    ("RearRight", 0.70, 0.33, 1.02, 0.47),
]

for name, hip_x, hip_y, foot_x, foot_y in leg_specs:
    side_sign = 1 if hip_y > 0 else -1
    hip = (hip_x, hip_y, 0.83)
    knee = ((hip_x + foot_x) * 0.5, foot_y * 0.92, 0.18)
    ankle = (foot_x, foot_y, -0.43)
    foot = (foot_x + (0.09 if foot_x > hip_x else -0.06), foot_y, -0.58)

    mesh_objects.append(sphere("RoboDog_%s_Hip_Disc" % name, hip, (0.15, 0.08, 0.15), silver, 24, 10))
    mesh_objects.append(sphere("RoboDog_%s_Hip_Inner" % name, (hip_x, hip_y + side_sign * 0.018, 0.83), (0.075, 0.035, 0.075), black, 16, 8))
    mesh_objects.append(cylinder_between("RoboDog_%s_Upper_Leg" % name, hip, knee, 0.062, silver, vertices=16))
    mesh_objects.append(sphere("RoboDog_%s_Knee_Joint" % name, knee, (0.105, 0.105, 0.105), dark_silver, 20, 10))
    mesh_objects.append(cylinder_between("RoboDog_%s_Lower_Black_Strut_A" % name, knee, ankle, 0.043, black, vertices=14))
    mesh_objects.append(cylinder_between("RoboDog_%s_Lower_Black_Strut_B" % name, (knee[0] + 0.045, knee[1], knee[2] - 0.03), (ankle[0] + 0.045, ankle[1], ankle[2] + 0.04), 0.026, deep_black, vertices=10))
    mesh_objects.append(sphere("RoboDog_%s_Ankle_Joint" % name, ankle, (0.078, 0.078, 0.078), dark_silver, 16, 8))
    mesh_objects.append(sphere("RoboDog_%s_Rubber_Paw" % name, foot, (0.16, 0.105, 0.075), black, 24, 10))

    for offset in [-0.052, 0.052]:
        mesh_objects.append(sphere("RoboDog_%s_Hip_Screw" % name, (hip_x, hip_y + side_sign * 0.07, 0.83 + offset), (0.018, 0.018, 0.018), black, 10, 5))

# Exposed underbody cables.
mesh_objects.append(cylinder_between("RoboDog_Underbody_Cable_Left", (-0.72, -0.21, 0.64), (0.76, -0.24, 0.56), 0.018, deep_black, vertices=10))
mesh_objects.append(cylinder_between("RoboDog_Underbody_Cable_Right", (-0.72, 0.21, 0.64), (0.76, 0.24, 0.56), 0.018, deep_black, vertices=10))

# Small side vents and sensor details.
for y in [-0.315, 0.315]:
    for i, x in enumerate([0.12, 0.24, 0.36]):
        mesh_objects.append(cube("RoboDog_Side_Vent_%02d" % (i + (1 if y < 0 else 10)), (x, y, 0.70), (0.035, 0.025, 0.14), deep_black, rotation=(0, 0, math.radians(-18)), bevel=0.008))
    for x in [-0.14, 0.02]:
        mesh_objects.append(cube("RoboDog_Rect_Port", (x, y, 0.91), (0.09, 0.026, 0.13), deep_black, bevel=0.012))

# Lights and preview camera.
bpy.ops.object.light_add(type="AREA", location=(-2.6, -3.2, 4.0), rotation=(math.radians(62), 0, math.radians(-32)))
key = bpy.context.object
key.name = "RoboDog_Preview_Key_Light"
key.data.energy = 560
key.data.size = 4.0

bpy.ops.object.light_add(type="POINT", location=(1.5, 2.2, 1.7))
rim = bpy.context.object
rim.name = "RoboDog_Preview_Rim_Light"
rim.data.energy = 180

bpy.ops.object.camera_add(location=(-3.4, -3.0, 1.3), rotation=(math.radians(72), 0, math.radians(-47)))
bpy.context.scene.camera = bpy.context.object

bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)

bpy.ops.object.select_all(action="DESELECT")
for obj in mesh_objects:
    if obj is not None and obj.type == "MESH":
        obj.select_set(True)

bpy.context.view_layer.objects.active = body
bpy.ops.export_scene.fbx(
    filepath=FBX_PATH,
    use_selection=True,
    object_types={"MESH"},
    apply_unit_scale=True,
    bake_space_transform=False,
)
