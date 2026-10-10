"""
Exports dreamWorld's code-built art (Props/Buildings/Critters .blend files) to FBX for Unity.
  blender.exe -b --factory-startup --python ArtSource/export_unity.py
Each object becomes its own FBX with its origin at the object's base (weapons: at the grip, blade
pointing +Y in Unity). Vertex colors are kept (sRGB); materials stay named Matte/Shiny/Glow/Glass and
Unity swaps them for the shared PSX vertex-color materials when the world is built.
"""
import math
import os

import bpy
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
OUT = os.path.join(ROOT, "Assets", "Models")

# weapon: (grip point in Blender coords, rotation that turns the blade direction into Blender +Z)
WEAPONS = {
    "Prop_BeetleHornSword": ((0, -0.12, 0.0), Matrix.Rotation(math.radians(90), 4, "X")),   # blade +Y -> +Z
    "Prop_MushroomMace": ((0, 0, 0.12), Matrix.Identity(4)),
    "Prop_CrystalStaff": ((0, 0, 0.55), Matrix.Identity(4)),
    "Prop_KeyboardHammer": ((0, 0, 0.1), Matrix.Identity(4)),
    "Prop_MantisScythe": ((0, 0, 0.25), Matrix.Identity(4)),
    "Prop_UmbrellaSword": ((0, 0, 0.06), Matrix.Identity(4)),
    "Prop_FryingPan": ((0.5, 0, 0.03), Matrix.Rotation(math.radians(90), 4, "Y")),         # pan -X -> +Z
    "Prop_Slingshot": ((0, 0, 0.08), Matrix.Identity(4)),
    "Prop_PetalShield": ((0, 0, 0.0), Matrix.Identity(4)),                                   # face +Z (Unity +Y)
}

PROPS = """Prop_StoneFountain Prop_MarketStall Prop_NoticeBoard Prop_CrookedSignpost Prop_BugHotel Prop_PicketFence
Prop_MushroomStreetlamp Prop_LanternString Prop_FireflyLantern Prop_GlowcapCluster Prop_MossyBoulder Prop_FernClump
Prop_CrystalCluster Prop_AutumnBush Prop_DuckStatue Prop_FrogMailbox Prop_SnailBench Prop_LostSockPile Prop_CoffinBed
Prop_BatCandelabra Prop_WitchCauldron Prop_SpookyTreeLamp Prop_GraveFlowers Prop_DressMannequin Prop_ClothesRack
Prop_SewingMachine Prop_FabricBolts Prop_FullMirror Prop_PlaidArmchair Prop_Bookshelf Prop_RoundRug Prop_CRTDesk
Prop_MushroomStool Prop_PottedMonstera Prop_FairyLightJar Prop_CRTStack Prop_MossTerrarium Prop_WelcomeMat
Prop_FlowerWindowBox Prop_VampireGnome Prop_VegetablePatch Prop_Hammock Prop_Campfire Prop_LeafArchway
Prop_MoonflowerLamp Prop_BugArcade Prop_Jukebox Prop_ClawMachine Prop_BeetleCart Prop_AntHill Prop_Caterpillar
Prop_SnailShellPlanter Prop_MossyServerRack Prop_OvergrownTerminal Prop_CableArch Prop_PlaidBanner Prop_WindChimes
Prop_BugSpecimenFrame Prop_StrawberryCake Prop_MushroomBread Prop_FruitCrate Prop_CandleCluster""".split()
BUILDINGS = "MushroomBakery GreenhouseArcade MossServerCafe StiltWatchtower BeetleShellHouse AquariumShop CircuitGroveShrine MossyArchBridge".split()
CRITTERS = "Critter_RainbowStagBeetle Critter_Ladybug Critter_LunaMoth Critter_Firefly Critter_MushroomSnail".split()


def export(objs, path):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={"MESH"},
                             apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y",
                             mesh_smooth_type="FACE", colors_type="SRGB", bake_anim=False, path_mode="STRIP")
    print("EXPORTED", os.path.relpath(path, ROOT))


def to_origin(obj, grip=(0, 0, 0), rot=Matrix.Identity(4), base_center=False):
    """Bakes the object's transform into its mesh so the FBX pivot is at the chosen point."""
    me = obj.data
    loc = obj.location.copy()
    obj.location = (0, 0, 0)
    bpy.context.view_layer.update()
    m = rot @ Matrix.Translation(-Vector(grip))
    if base_center:   # props: center x/y on the footprint, sit on z = 0
        xs = [v.co.x for v in me.vertices]
        ys = [v.co.y for v in me.vertices]
        zs = [v.co.z for v in me.vertices]
        m = Matrix.Translation((-(min(xs) + max(xs)) / 2, -(min(ys) + max(ys)) / 2, -min(zs)))
    me.transform(m)
    me.update()
    return loc


def run():
    # weapons + props + enemies
    bpy.ops.wm.open_mainfile(filepath=os.path.join(HERE, "Props.blend"))
    for o in list(bpy.data.objects):
        if o.type != "MESH":
            continue
        if o.name in WEAPONS:
            grip, rot = WEAPONS[o.name]
            to_origin(o, grip, rot)
            export([o], os.path.join(OUT, "Weapons", o.name.replace("Prop_", "") + ".fbx"))
        elif o.name.startswith("Enemy_"):
            to_origin(o, base_center=True)
            export([o], os.path.join(OUT, "Enemies", o.name.replace("Enemy_", "") + ".fbx"))
        elif o.name in PROPS:
            to_origin(o, base_center=True)
            export([o], os.path.join(OUT, "Props", o.name.replace("Prop_", "") + ".fbx"))

    # buildings: body + roof in one file, keeping their shared origin (doorway faces +Y in Blender)
    bpy.ops.wm.open_mainfile(filepath=os.path.join(HERE, "Buildings.blend"))
    for name in BUILDINGS:
        body = bpy.data.objects["Bld_" + name]
        roof = bpy.data.objects.get("Bld_" + name + "_Roof")
        parts = [body] + ([roof] if roof else [])
        for o in parts:
            o.location = (0, 0, 0)
        export(parts, os.path.join(OUT, "Buildings", name + ".fbx"))

    # a few harmless ambient critters
    bpy.ops.wm.open_mainfile(filepath=os.path.join(HERE, "Critters.blend"))
    for name in CRITTERS:
        o = bpy.data.objects.get(name)
        if o:
            to_origin(o, base_center=True)
            export([o], os.path.join(OUT, "Critters", name.replace("Critter_", "") + ".fbx"))


run()
