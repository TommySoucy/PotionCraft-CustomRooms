using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using UnityEngine;
using System.Reflection;
using BepInEx;
using ObjectBased.UIElements;
using UnityEngine.Events;
using ObjectBased;
using Markers;

namespace CustomRooms
{
    [BepInPlugin(pluginGuid, pluginName, pluginVersion)]
    public class CustomRoomsMod : BaseUnityPlugin
    {
        // BepinEx
        public const string pluginGuid = "VIP.TommySoucy.CustomRooms";
        public const string pluginName = "CustomRooms";
        public const string pluginVersion = "1.0.0";

        // Live data
        public static CustomRoomsMod modInstance;
        public static AssetBundle assetBundle;
        public static Dictionary<Vector2Int, Room> rooms;
        public static Dictionary<Vector2Int, Sprite[]> navigationButtons;
        public static Sprite[] defaultNavigationButtons; // 0 is Up idle, 1 is Up hover, then continue clockwise
        public static Vector2Int currentRoomCoords;
        public static Vector2Int targetRoomCoords;
        public static bool roomCoordsInitialized = false;
        public static Vector2? cornerPos;
        public static List<String> customRoomNames;

        public void Awake()
        {
            modInstance = this;

            LoadAssets();

            DoPatching();
        }

        private void LoadAssets()
        {
            // Load mod's AssetBundle
            assetBundle = AssetBundle.LoadFromFile("BepinEx/Plugins/Rooms/CustomRooms.ab");

            // Initialize default navigation button sprites
            defaultNavigationButtons = new Sprite[8];
            defaultNavigationButtons[0] = assetBundle.LoadAsset<Sprite>("NavigationButton_Up_Idle");
            defaultNavigationButtons[1] = assetBundle.LoadAsset<Sprite>("NavigationButton_Up_Hover");
            defaultNavigationButtons[2] = assetBundle.LoadAsset<Sprite>("NavigationButton_Right_Idle");
            defaultNavigationButtons[3] = assetBundle.LoadAsset<Sprite>("NavigationButton_Right_Hover");
            defaultNavigationButtons[4] = assetBundle.LoadAsset<Sprite>("NavigationButton_Down_Idle");
            defaultNavigationButtons[5] = assetBundle.LoadAsset<Sprite>("NavigationButton_Down_Hover");
            defaultNavigationButtons[6] = assetBundle.LoadAsset<Sprite>("NavigationButton_Left_Idle");
            defaultNavigationButtons[7] = assetBundle.LoadAsset<Sprite>("NavigationButton_Left_Hover");
        }

        private void DoPatching()
        {
            var harmony = new HarmonyLib.Harmony("VIP.TommySoucy.CustomRooms");

            // LoadRoomsPatch
            var loadRoomsPatchOriginal = typeof(RoomManager).GetMethod("LoadRooms", BindingFlags.NonPublic | BindingFlags.Instance);
            var loadRoomsPatchPrefix = typeof(LoadRoomsPatch).GetMethod("Prefix", BindingFlags.NonPublic | BindingFlags.Static);
            var loadRoomsPatchPostfix = typeof(LoadRoomsPatch).GetMethod("Postfix", BindingFlags.NonPublic | BindingFlags.Static);

            harmony.Patch(loadRoomsPatchOriginal, new HarmonyMethod(loadRoomsPatchPrefix), new HarmonyMethod(loadRoomsPatchPostfix));

            // RoomManagerOnLoadPatch
            var roomManagerOnLoadPatchOriginal = typeof(RoomManager).GetMethod("OnLoad", BindingFlags.NonPublic | BindingFlags.Instance);
            var roomManagerOnLoadPatchPostfix = typeof(RoomManagerOnLoadPatch).GetMethod("Prefix", BindingFlags.NonPublic | BindingFlags.Static);

            harmony.Patch(roomManagerOnLoadPatchOriginal, new HarmonyMethod(roomManagerOnLoadPatchPostfix));

            // ChangeRoomByDirectionPatch
            var changeRoomByDirectionPatchOriginal = typeof(RoomManager).GetMethod("ChangeRoomByDirection");
            var changeRoomByDirectionPatchPrefix = typeof(ChangeRoomByDirectionPatch).GetMethod("Prefix", BindingFlags.NonPublic | BindingFlags.Static);

            harmony.Patch(changeRoomByDirectionPatchOriginal, new HarmonyMethod(changeRoomByDirectionPatchPrefix));

            // OnNewRoomTargetPatch
            var onNewRoomTargetPatchOriginal = typeof(NavigationButton).GetMethod("OnNewRoomTarget", BindingFlags.NonPublic | BindingFlags.Instance);
            var onNewRoomTargetPatchPrefix = typeof(OnNewRoomTargetPatch).GetMethod("Prefix", BindingFlags.NonPublic | BindingFlags.Static);

            harmony.Patch(onNewRoomTargetPatchOriginal, new HarmonyMethod(onNewRoomTargetPatchPrefix));

            // MoveToRoomPatch
            var moveToRoomPatchOriginal = typeof(CameraMover).GetMethod("MoveToRoom");
            var moveToRoomPatchPrefix = typeof(MoveToRoomPatch).GetMethod("Prefix", BindingFlags.NonPublic | BindingFlags.Static);

            harmony.Patch(moveToRoomPatchOriginal, new HarmonyMethod(moveToRoomPatchPrefix));
        }

        public void LogError(string error)
        {
            Logger.LogError(error);
        }

        public void LogWarning(string warning)
        {
            Logger.LogWarning(warning);
        }

        public void LogInfo(string info)
        {
            Logger.LogInfo(info);
        }
    }

    // Patches RoomManager.LoadRooms() so we can load our rooms into the settings too before it instantiates all the rooms' prefabs
    class LoadRoomsPatch
    {
        static void Prefix(ref RoomManagerSettings ___settings)
        {
            CustomRoomsMod.rooms = new Dictionary<Vector2Int, Room>();
            CustomRoomsMod.navigationButtons = new Dictionary<Vector2Int, Sprite[]>();
            CustomRoomsMod.customRoomNames = new List<string>();

            // Get private Room.сamPos field so we can set it manually for each room later
            // NOTE: The literal "сamPos" seems to use a special character so the literal had to be copied from the source instead of typed
            FieldInfo roomCamPosField = typeof(Room).GetField("сamPos", BindingFlags.NonPublic | BindingFlags.Instance);

            // Load rooms from file
            // Current game rooms array will only be of the size needed to contain vanilla rooms, so need to remake that array entirely
            List<Room> newRooms = new List<Room>();
            newRooms.AddRange(___settings.rooms); // Start by adding vanilla rooms to the list so they're still first

            // Also add vanilla rooms to our dictionnary
            foreach (Room vanillaRoom in ___settings.rooms)
            {
                switch (vanillaRoom.name)
                {
                    case "LabRoom":
                        CustomRoomsMod.rooms.Add(Vector2Int.zero, vanillaRoom);
                        LoadVanillaNavigationButtons(Vector2Int.zero, "Lab");
                        break;
                    case "GardenRoom":
                        CustomRoomsMod.rooms.Add(Vector2Int.right, vanillaRoom);
                        LoadVanillaNavigationButtons(Vector2Int.right, "Garden");
                        break;
                    case "MeetingRoom":
                        CustomRoomsMod.rooms.Add(Vector2Int.left, vanillaRoom);
                        LoadVanillaNavigationButtons(Vector2Int.left, "Meeting");
                        break;
                    case "BedroomRoom":
                        CustomRoomsMod.rooms.Add(Vector2Int.up, vanillaRoom);
                        LoadVanillaNavigationButtons(Vector2Int.up, "Bedroom");
                        break;
                    case "BasementRoom":
                        CustomRoomsMod.rooms.Add(Vector2Int.down, vanillaRoom);
                        LoadVanillaNavigationButtons(Vector2Int.down, "Basement");
                        break;
                    default:
                        CustomRoomsMod.modInstance.LogWarning("Non-vanilla room found in vanilla rooms list");
                        break;
                }
            }

            string[] roomFiles = Directory.GetFiles("BepinEx/Plugins/Rooms");
            foreach (string roomPath in roomFiles)
            {
                string[] splitOnPeriod = roomPath.Split('.'); // Last string should be extension

                // Check file extension, we want to process text files
                if (splitOnPeriod[splitOnPeriod.Length - 1].Equals("txt", StringComparison.OrdinalIgnoreCase))
                {
                    string[] lines = File.ReadAllLines(roomPath);

                    // Parse room settings
                    Vector2Int roomCoords = Vector2Int.zero;
                    string prefabName = "";
                    foreach (string line in lines)
                    {
                        if (line.Length == 0 || line[0] == '#')
                        {
                            continue;
                        }

                        string trimmedLine = line.Trim();
                        string[] tokens = trimmedLine.Split('=');

                        if (tokens.Length == 0)
                        {
                            continue;
                        }

                        if (tokens[0].IndexOf("roomCoordsX") == 0)
                        {
                            roomCoords.x = int.Parse(tokens[1].Trim());
                        }
                        else if (tokens[0].IndexOf("roomCoordsY") == 0)
                        {
                            roomCoords.y = int.Parse(tokens[1].Trim());
                        }
                        else if (tokens[0].IndexOf("prefabName") == 0)
                        {
                            prefabName = tokens[1].Trim();
                        }
                    }

                    // Deal with whether room coords were specified or not
                    if (!roomCoords.Equals(Vector2Int.zero) && !CustomRoomsMod.rooms.ContainsKey(roomCoords))
                    {
                        // Load room prefab and create room instance
                        // Get path to assetbundle, should be same as room descriptor file but .ab extension
                        int extensionIndex = roomPath.IndexOf(".txt", StringComparison.OrdinalIgnoreCase);
                        AssetBundle assetBundle = AssetBundle.LoadFromFile(roomPath.Substring(0, extensionIndex) + ".ab");

                        if (assetBundle != null)
                        {
                            GameObject roomPrefab = assetBundle.LoadAsset<GameObject>(prefabName.Equals("") ? "RoomPrefab" : prefabName);
                            CustomRoomsMod.customRoomNames.Add(roomPrefab.name);

                            if (roomPrefab != null)
                            {
                                Room newRoom = ScriptableObject.CreateInstance<Room>();
                                newRoom.prefab = roomPrefab;
                                Vector2 camPos = new Vector2(roomCoords.x * 25.6f, roomCoords.y * 14.4f);
                                roomCamPosField.SetValue(newRoom, camPos);
                                CustomRoomsMod.rooms.Add(roomCoords, newRoom);
                                newRooms.Add(newRoom);
                                CustomRoomsMod.modInstance.LogInfo("Loaded custom room: "+ roomPrefab.name);

                                // Get navigation button sprites
                                Sprite[] navigationButtons = new Sprite[8];
                                navigationButtons[0] = assetBundle.LoadAsset<Sprite>("NavigationButton_Up_Idle");
                                navigationButtons[1] = assetBundle.LoadAsset<Sprite>("NavigationButton_Up_Hover");
                                navigationButtons[2] = assetBundle.LoadAsset<Sprite>("NavigationButton_Right_Idle");
                                navigationButtons[3] = assetBundle.LoadAsset<Sprite>("NavigationButton_Right_Hover");
                                navigationButtons[4] = assetBundle.LoadAsset<Sprite>("NavigationButton_Down_Idle");
                                navigationButtons[5] = assetBundle.LoadAsset<Sprite>("NavigationButton_Down_Hover");
                                navigationButtons[6] = assetBundle.LoadAsset<Sprite>("NavigationButton_Left_Idle");
                                navigationButtons[7] = assetBundle.LoadAsset<Sprite>("NavigationButton_Left_Hover");
                                CustomRoomsMod.navigationButtons.Add(roomCoords, navigationButtons);
                            }
                            else
                            {
                                CustomRoomsMod.modInstance.LogError("Could not load RoomPrefab from assetbundle of: " + roomPath);
                            }
                        }
                        else
                        {
                            CustomRoomsMod.modInstance.LogError("Could not find assetBundle for: " + roomPath);
                        }

                        // Unload room's asset bundle
                        assetBundle.Unload(false);
                    }
                    else
                    {
                        CustomRoomsMod.modInstance.LogError("Room coords not specified or room already exists at: " + roomCoords);
                    }
                }
            }

            // Set new room array
            ___settings.rooms = newRooms.ToArray();
        }

        static void Postfix(ref GameObject[] ___instantiatedRooms)
        {
            // Set the position of every custom room object instance to the same as сamPos
            for (int i = 5; i < ___instantiatedRooms.Length; ++i)
            {
                Vector2 camPos = Managers.Room.settings.rooms[i].GetCamPos();
                ___instantiatedRooms[i].transform.position = new Vector3(camPos.x, camPos.y, 0);
            }

            // Replace vanilla walls with the new walls because vanilla ones can interfere with new rooms
            GameObject.Destroy(GameObject.Find("Walls"));
            GameObject wallsInstance = GameObject.Instantiate(CustomRoomsMod.assetBundle.LoadAsset<GameObject>("Walls"));
            wallsInstance.name = "Walls";
            foreach (Transform transform in wallsInstance.transform) // Walls are in sets for each room
            {
                if (transform.name.Equals("Meeting"))
                {
                    // Meeting room needs specific "Markers" components added to specific walls
                    // GroundFloor on Bottom, used by scales
                    // LeftWall on Left, used by trader menu
                    foreach (Transform colTransform in transform) // Each set has a collider for each side
                    {
                        GameObject obj = colTransform.gameObject;
                        obj.layer = Layers.Surfaces;
                        switch (obj.name)
                        {
                            case "Left":
                                obj.AddComponent<LeftWall>();
                                break;
                            case "Bottom":
                                BoxCollider2D col = obj.GetComponent<BoxCollider2D>();
                                GroundFloor groundFloorMarker = obj.AddComponent<GroundFloor>();
                                groundFloorMarker.boxCollider = col;
                                SurfaceCollider sc = obj.AddComponent<SurfaceCollider>();
                                sc.mainCollider = col;
                                sc.pushOutSide = SurfaceCollider.PushOutSide.Up;
                                break;
                            default:
                                break;
                        }
                    }

                }
                else
                {
                    foreach (Transform colTransform in transform) 
                    {
                        GameObject obj = colTransform.gameObject;
                        obj.layer = Layers.Surfaces;
                        if (obj.name.Equals("Bottom"))
                        {
                            Collider2D col = obj.GetComponent<Collider2D>();
                            SurfaceCollider sc = obj.AddComponent<SurfaceCollider>();
                            sc.mainCollider = col;
                            sc.pushOutSide = SurfaceCollider.PushOutSide.Up;
                        }
                    }
                }
            }

            // Set layers of walls on custom rooms because these can't be set in prefab, so they have to be set once the rooms' prefabs are instantiated
            foreach(String roomName in CustomRoomsMod.customRoomNames)
            {
                GameObject room = GameObject.Find(roomName);
                if(room != null)
                {
                    // Find Col transform
                    foreach(Transform transform in room.transform)
                    {
                        if (transform.name.Equals("Col"))
                        {
                            foreach(Transform colTransform in transform)
                            {
                                colTransform.gameObject.layer = Layers.Surfaces;
                            }
                            break;
                        }
                    }
                }
            }

            // Remove vanilla garden ground collider, it is now included in the custom walls prefab
            GameObject garden = GameObject.Find("Room Garden");
            foreach(Transform transform in garden.transform)
            {
                if(transform.name.Equals("Collider Floor"))
                {
                    GameObject.Destroy(transform.gameObject);
                    break;
                }
            }
        }

        private static void LoadVanillaNavigationButtons(Vector2Int roomCoords, string roomName)
        {
            Sprite[] navigationButtons = new Sprite[8];
            navigationButtons[0] = CustomRoomsMod.assetBundle.LoadAsset<Sprite>("NavigationButton_" + roomName + "_Up_Idle");
            navigationButtons[1] = CustomRoomsMod.assetBundle.LoadAsset<Sprite>("NavigationButton_" + roomName + "_Up_Hover");
            navigationButtons[2] = CustomRoomsMod.assetBundle.LoadAsset<Sprite>("NavigationButton_" + roomName + "_Right_Idle");
            navigationButtons[3] = CustomRoomsMod.assetBundle.LoadAsset<Sprite>("NavigationButton_" + roomName + "_Right_Hover");
            navigationButtons[4] = CustomRoomsMod.assetBundle.LoadAsset<Sprite>("NavigationButton_" + roomName + "_Down_Idle");
            navigationButtons[5] = CustomRoomsMod.assetBundle.LoadAsset<Sprite>("NavigationButton_" + roomName + "_Down_Hover");
            navigationButtons[6] = CustomRoomsMod.assetBundle.LoadAsset<Sprite>("NavigationButton_" + roomName + "_Left_Idle");
            navigationButtons[7] = CustomRoomsMod.assetBundle.LoadAsset<Sprite>("NavigationButton_" + roomName + "_Left_Hover");
            CustomRoomsMod.navigationButtons.Add(roomCoords, navigationButtons);
        }
    }

    // Patches RoomManager.OnLoad() to know which room we were loaded in
    class RoomManagerOnLoadPatch
    {
        static void Prefix()
        {
            // Find which vanilla room we load in (If saved in custom room, will spawn in lab because can't calculate custom room's coords based on roomIndex. Would need custom save)
            switch (Managers.SaveLoad.SelectedProgressState.currentRoom) // We check targetRoom instead of currentRoom because currentRoom will not have been set yet
            {
                case RoomManager.RoomIndex.Basement:
                    CustomRoomsMod.currentRoomCoords = Vector2Int.down;
                    break;
                case RoomManager.RoomIndex.Bedroom:
                    CustomRoomsMod.currentRoomCoords = Vector2Int.up;
                    break;
                case RoomManager.RoomIndex.Garden:
                    CustomRoomsMod.currentRoomCoords = Vector2Int.right;
                    break;
                case RoomManager.RoomIndex.Laboratory:
                    CustomRoomsMod.currentRoomCoords = Vector2Int.zero;
                    break;
                case RoomManager.RoomIndex.Meeting:
                    CustomRoomsMod.currentRoomCoords = Vector2Int.left;
                    break;
                case (RoomManager.RoomIndex)5: // Invalid room index used when in custom room, just load in lab
                    Managers.SaveLoad.SelectedProgressState.currentRoom = RoomManager.RoomIndex.Laboratory;
                    CustomRoomsMod.currentRoomCoords = Vector2Int.zero;
                    break;
                default:
                    CustomRoomsMod.modInstance.LogWarning("Loaded in room with invalid RoomIndex: "+(int)(Managers.Room.targetRoom));
                    Managers.SaveLoad.SelectedProgressState.currentRoom = RoomManager.RoomIndex.Laboratory;
                    CustomRoomsMod.currentRoomCoords = Vector2Int.zero;
                    break;
            }

            CustomRoomsMod.roomCoordsInitialized = true;
        }
    }

    // Patches RoomManager.ChangeRoomByDirection(Vector2 direction) to handle a move to/from a custom rooms
    class ChangeRoomByDirectionPatch
    {
        static bool Prefix(Vector2 direction, ref RoomManager.RoomIndex ___targetRoom, ref CameraMover ____cameraMover)
        {
            Vector2Int directionInt = new Vector2Int(Mathf.RoundToInt(direction.x), Mathf.RoundToInt(direction.y));
            Vector2Int destinationCoords = CustomRoomsMod.currentRoomCoords + directionInt;
            if (CustomRoomsMod.roomCoordsInitialized)
            {
                // Check if destination exists
                if (CustomRoomsMod.rooms.ContainsKey(destinationCoords))
                {
                    // This replaces the original GoTo() method entirely and uses coords instead of RoomIndex
                    DarkScreen.DeactivateAllActiveObjects(DarkScreen.DeactivationType.Other);
                    if (____cameraMover.IsMoving() && destinationCoords == CustomRoomsMod.targetRoomCoords)
                    {
                        return false;
                    }
                    bool cameraMoving = ____cameraMover.IsMoving();
                    bool currentRoomIsTarget = CustomRoomsMod.currentRoomCoords == destinationCoords;
                    Room room = CustomRoomsMod.rooms[CustomRoomsMod.targetRoomCoords]; // Current target
                    Room destinationRoom = CustomRoomsMod.rooms[destinationCoords]; // New target, will replace current target
                    bool moveOnNextFrame = false;

                    if (cameraMoving)
                    {
                        FieldInfo cornerPosField = typeof(CameraMover).GetField("cornerPos", BindingFlags.NonPublic | BindingFlags.Instance);
                        Vector2? cornerPos = (Vector2?)cornerPosField.GetValue(____cameraMover);

                        if (cornerPos == null)
                        {
                            CustomRoomsMod.cornerPos = room.GetCamPos();
                        }
                        else
                        {
                            if (!CanGoStraight(____cameraMover.Position, destinationRoom.GetCamPos()))
                            {
                                if (!room.GetCamPos().Equals(cornerPos))
                                {
                                    moveOnNextFrame = true;
                                }
                            }
                        }

                        UnityEvent onStopBeingTargetEvent = room.onStopBeingTargetEvent;
                        if (onStopBeingTargetEvent != null)
                        {
                            onStopBeingTargetEvent.Invoke();
                        }
                    }
                    Room currentRoom = CustomRoomsMod.rooms[CustomRoomsMod.currentRoomCoords];
                    CustomRoomsMod.targetRoomCoords = destinationCoords;
                    if(destinationCoords.magnitude <= 1) // check if room is vanilla, if it is we have to set target room index
                    {
                        if (destinationCoords.Equals(Vector2Int.zero))
                        {
                            ___targetRoom = (RoomManager.RoomIndex)1;
                        }
                        else if(destinationCoords.Equals(Vector2Int.up))
                        {
                            ___targetRoom = (RoomManager.RoomIndex)4;
                        }
                        else if(destinationCoords.Equals(Vector2Int.right))
                        {
                            ___targetRoom = (RoomManager.RoomIndex)2;
                        }
                        else if(destinationCoords.Equals(Vector2Int.down))
                        {
                            ___targetRoom = (RoomManager.RoomIndex)3;
                        }
                        else if(destinationCoords.Equals(Vector2Int.left))
                        {
                            ___targetRoom = (RoomManager.RoomIndex)0;
                        }
                    }
                    else
                    {
                        ___targetRoom = (RoomManager.RoomIndex)5; // If we are not in vanilla room, set invalid index so navigation buttons get updated properly when loading a save 
                    }
                    ____cameraMover.MoveToRoom(destinationRoom.GetCamPos(), moveOnNextFrame);
                    if (!currentRoomIsTarget || cameraMoving)
                    {
                        destinationRoom.onBecomeTargetEvent.Invoke();
                    }
                    if (!currentRoomIsTarget)
                    {
                        currentRoom.onMoveTakeOffEvent.Invoke();
                    }

                    CustomRoomsMod.currentRoomCoords = destinationCoords;

                    return false;
                }
                else // Room doesn't exist, do the same as original and just return
                {
                    return false;
                }
            }

            return true;
        }

        // Copied from source
        public static bool CanGoStraight(Vector2 pos, Vector2 toPos)
        {
            Vector2 position = pos;
            if (!position.x.Equals(toPos.x))
            {
                position = pos;
                if (!position.y.Equals(toPos.y))
                {
                    return false;
                }
            }
            return true;
        }
    }

    // Patches NavigationButton.OnNewRoomTarget, replaces it entirely
    class OnNewRoomTargetPatch
    {
        static bool Prefix(ref NavigationButton __instance, int index, ref NavigationButton.Side ___side)
        {
            Room room = Managers.Room.settings.rooms[index];
            Vector2 camPos = room.GetCamPos();
            Vector2Int roomCoords = new Vector2Int(Mathf.RoundToInt(camPos.x / 25.6f), Mathf.RoundToInt(camPos.y / 14.4f));

            MethodInfo setSpriteMethod = typeof(NavigationButton).GetMethod("SetSprite", BindingFlags.NonPublic | BindingFlags.Instance);

            Vector2Int nearestInDirection;
            switch (___side)
            {
                case NavigationButton.Side.Up:
                    if (FindNextRoomInDirection(roomCoords, Vector2Int.up, out nearestInDirection))
                    {
                        // Enable by setting sprite
                        // In original method, sprites are hardcoded for current room. Since custom rooms can be added, we need to fetch sprites for that specific room

                        // Handle vanilla room's preexisting sprites exactly like original
                        if (index == 3 || index == 1)
                        {
                            VanillaEnableButton(__instance, index);
                        }
                        else // Room in up direction does not exist in vanilla, have to fetch custom room's button
                        {
                            Sprite[] navigationButtons;
                            if (CustomRoomsMod.navigationButtons.ContainsKey(nearestInDirection)) 
                            {
                                navigationButtons = CustomRoomsMod.navigationButtons[nearestInDirection];
                            }
                            else // If entry missing for some reason, use default
                            {
                                navigationButtons = CustomRoomsMod.defaultNavigationButtons;
                            }

                            if (navigationButtons[0] == null || navigationButtons[1] == null)
                            {
                                navigationButtons[0] = CustomRoomsMod.defaultNavigationButtons[0];
                                navigationButtons[1] = CustomRoomsMod.defaultNavigationButtons[1];
                            }

                            CustomEnableButton(__instance, navigationButtons, 0);
                        }
                    }
                    else
                    {
                        setSpriteMethod.Invoke(__instance, new object[] { null });
                    }
                    break;
                case NavigationButton.Side.Down:
                    if (FindNextRoomInDirection(roomCoords, Vector2Int.down, out nearestInDirection))
                    {
                        if (index == 1 || index == 4)
                        {
                            VanillaEnableButton(__instance, index);
                        }
                        else
                        {
                            Sprite[] navigationButtons;
                            if (CustomRoomsMod.navigationButtons.ContainsKey(nearestInDirection))
                            {
                                navigationButtons = CustomRoomsMod.navigationButtons[nearestInDirection];
                            }
                            else // If nearestInDirection is a vanilla room we wont have an entry
                            {
                                navigationButtons = CustomRoomsMod.defaultNavigationButtons;
                            }

                            if (navigationButtons[4] == null || navigationButtons[5] == null)
                            {
                                navigationButtons[4] = CustomRoomsMod.defaultNavigationButtons[4];
                                navigationButtons[5] = CustomRoomsMod.defaultNavigationButtons[5];
                            }

                            CustomEnableButton(__instance, navigationButtons, 4);
                        }
                    }
                    else
                    {
                        setSpriteMethod.Invoke(__instance, new object[] { null });
                    }
                    break;
                case NavigationButton.Side.Left:
                    if (FindNextRoomInDirection(roomCoords, Vector2Int.left, out nearestInDirection))
                    {
                        if (index == 1 || index == 2)
                        {
                            VanillaEnableButton(__instance, index);
                        }
                        else
                        {
                            Sprite[] navigationButtons;
                            if (CustomRoomsMod.navigationButtons.ContainsKey(nearestInDirection))
                            {
                                navigationButtons = CustomRoomsMod.navigationButtons[nearestInDirection];
                            }
                            else // If nearestInDirection is a vanilla room we wont have an entry
                            {
                                navigationButtons = CustomRoomsMod.defaultNavigationButtons;
                            }

                            if (navigationButtons[6] == null || navigationButtons[7] == null)
                            {
                                navigationButtons[6] = CustomRoomsMod.defaultNavigationButtons[6];
                                navigationButtons[7] = CustomRoomsMod.defaultNavigationButtons[7];
                            }

                            CustomEnableButton(__instance, navigationButtons, 6);
                        }
                    }
                    else
                    {
                        setSpriteMethod.Invoke(__instance, new object[] { null });
                    }
                    break;
                case NavigationButton.Side.Right:
                    if (FindNextRoomInDirection(roomCoords, Vector2Int.right, out nearestInDirection))
                    {
                        if (index == 1 || index == 0)
                        {
                            VanillaEnableButton(__instance, index);
                        }
                        else
                        {
                            Sprite[] navigationButtons;
                            if (CustomRoomsMod.navigationButtons.ContainsKey(nearestInDirection))
                            {
                                navigationButtons = CustomRoomsMod.navigationButtons[nearestInDirection];
                            }
                            else // If nearestInDirection is a vanilla room we wont have an entry
                            {
                                navigationButtons = CustomRoomsMod.defaultNavigationButtons;
                            }

                            if (navigationButtons[2] == null || navigationButtons[3] == null)
                            {
                                navigationButtons[2] = CustomRoomsMod.defaultNavigationButtons[2];
                                navigationButtons[3] = CustomRoomsMod.defaultNavigationButtons[3];
                            }

                            CustomEnableButton(__instance, navigationButtons, 2);
                        }
                    }
                    else
                    {
                        setSpriteMethod.Invoke(__instance, new object[] { null });
                    }
                    break;
                default:
                    break;
            }

            return false;
        }

        static void CustomEnableButton(NavigationButton instance, Sprite[] navigationButtons, int directionIndex)
        {
            bool idle = instance.spriteRenderer.sprite == instance.normalSprite;

            instance.normalSprite = navigationButtons[directionIndex];
            instance.hoveredSprite = navigationButtons[directionIndex + 1];
            instance.pressedSprite = navigationButtons[directionIndex + 1];

            Sprite sprite = idle ? instance.normalSprite : instance.hoveredSprite;

            MethodInfo setSpriteMethod = typeof(NavigationButton).GetMethod("SetSprite", BindingFlags.NonPublic | BindingFlags.Instance);
            setSpriteMethod.Invoke(instance, new object[] { sprite });
        }

        // This is pretty much an exact copy of NavigationButton.<OnNewRoomTarget>g__Enabled|23_0
        static void VanillaEnableButton(NavigationButton instance, int index)
        {
            int num = 0;
            if (instance.spriteRenderer.sprite == instance.normalSprite)
            {
                num = 0;
            }
            else if (instance.spriteRenderer.sprite == instance.hoveredSprite)
            {
                num = 1;
            }
            else if (instance.spriteRenderer.sprite == instance.pressedSprite)
            {
                num = 2;
            }
            instance.normalSprite = instance.spritesNormal[index];
            instance.hoveredSprite = instance.spritesHovered[index];
            instance.pressedSprite = instance.spritesPressed[index];
            Sprite sprite;
            if (num == 0)
            {
                sprite = instance.normalSprite;
            }
            else if (num == 1)
            {
                sprite = instance.hoveredSprite;
            }
            else
            {
                sprite = instance.pressedSprite;
            }

            MethodInfo setSpriteMethod = typeof(NavigationButton).GetMethod("SetSprite", BindingFlags.NonPublic | BindingFlags.Instance);
            setSpriteMethod.Invoke(instance, new object[] { sprite });
        }

        static bool FindNextRoomInDirection(Vector2Int from, Vector2Int direction, out Vector2Int nearestInDirection)
        {
            bool found = false;
            nearestInDirection = Vector2Int.zero;
            int nearestDistance = int.MaxValue;

            // We could iterate literally in the coord of direction, but there can be space between rooms, so how far should we go?
            // So instead i iterate through all rooms, and check if we hit something in the specified direction
            // Slower but can't fail
            foreach(KeyValuePair<Vector2Int, Room> roomEntry in CustomRoomsMod.rooms)
            {
                Vector2Int coords = roomEntry.Key;

                // Make coords relative to from coords
                Vector2Int relativeCoords = coords - from;

                // Check if in direction
                if (relativeCoords.x != 0 && direction.x != 0 && relativeCoords.y == 0 && Mathf.Sign(relativeCoords.x) == direction.x) // No need to get the sign of direction coord, it will already be unit
                {
                    // Set nearest if this is nearest to from
                    int distance = Mathf.Abs(relativeCoords.x);
                    if (distance < nearestDistance)
                    {
                        nearestDistance = distance;
                        nearestInDirection = coords;
                    }

                    found = true;
                }
                else if (relativeCoords.y != 0 && direction.y != 0 && relativeCoords.x == 0 && Mathf.Sign(relativeCoords.y) == direction.y)
                {
                    int distance = Mathf.Abs(relativeCoords.y);
                    if (distance < nearestDistance)
                    {
                        nearestDistance = distance;
                        nearestInDirection = coords;
                    }

                    found = true;
                }
            }

            return found;
        }
    }

    // Patches CameraMover.MoveToRoom
    class MoveToRoomPatch
    {
        static bool Prefix(ref CameraMover __instance, Vector2 targetPos, bool onNextUpdate,
                           ref Vector2? ___cornerPos, ref Vector2 ___startPos, ref Vector2 ___finishPos, ref float ___startTime, ref bool ___move)
        {
            if (onNextUpdate)
            {
                ___cornerPos = null;
                ___startPos = __instance.Position;
                ___finishPos = targetPos;
                __instance.Position = ___finishPos;
                ___startTime = 0f;
            }
            else
            {
                ___startPos = __instance.Position;
                if (ChangeRoomByDirectionPatch.CanGoStraight(__instance.Position, targetPos))
                {
                    ___cornerPos = null;
                }
                else
                {
                    if (CustomRoomsMod.cornerPos != null)
                    {
                        ___cornerPos = CustomRoomsMod.cornerPos;
                        CustomRoomsMod.cornerPos = null;
                    }
                    else
                    {
                        ___cornerPos = new Vector2?(Vector2.zero);
                    }
                }
                ___finishPos = targetPos;
                ___startTime = Time.time;
            }
            ___move = true;

            return false;
        }
    }
}
