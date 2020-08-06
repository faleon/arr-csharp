using BepInEx;
using UnityEngine;
using System;
using System.Text;
using SysForms = System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.SqlServer.Server;

namespace SDtD_Plugin
{
    [BepInPlugin("org.bepinex.plugins.sdtdplugin", "7 Days to Die Plug-in", "1.0.0.0")]
    [BepInProcess("7DaysToDie.exe")]
    public class SDtDPlugin : BaseUnityPlugin
    {
        private static GameObject Load = new GameObject();

        // Awake is called once when both the game and the plug-in are loaded
        void Awake()
        {
            UnityEngine.Debug.Log("Plug-in loaded successfully!");
            SDtDPlugin.Load.AddComponent<Main>();
            UnityEngine.Object.DontDestroyOnLoad(SDtDPlugin.Load);
        }
    }

    public class Main : MonoBehaviour
    {
        private vp_FPCamera fpCam;

        // Textures/colors
        private Texture2D crosshairTexture;
        private Texture2D enemyTexture;
        private Texture2D neutralTexture;
        private Texture2D lootTexture;

        // Cache
        private EntityEnemy[] cachedEnemies;
        private EntityAnimal[] cachedAnimals;
        private EntityNPC[] cachedNPCs;
        //private BlockLoot[] lootBlock;
        //private BlockSecureLoot[] lootSecure;

        private int updateInterval;
        private bool showDebugMsg;
        private string debugMsg;

        private Rect windowRect;
        private bool toggleWindow;
        
        private Camera cam;

        private float espRange;
        private float aimRange;
        private int aimSpeed;

        void Awake()
        {
            windowRect = new Rect(20, 20, 250, 750);
            toggleWindow = false;
            crosshairTexture = new Texture2D(128, 128);
            enemyTexture = new Texture2D(128, 128);
            neutralTexture = new Texture2D(128, 128);
            lootTexture = new Texture2D(128, 128);
            espRange = 200;
            aimRange = 150;
            aimSpeed = 7;
            cam = null;
            updateInterval = 3;
            showDebugMsg = false;
            debugMsg = "";
        }
        
        void Start()
        {
            //set crosshair color
            for (int y = 0; y < crosshairTexture.height; y++)
            {
                for (int x = 0; x < crosshairTexture.width; x++)
                {
                    Color color = (x & y) != 0 ? Color.yellow : Color.yellow;
                    crosshairTexture.SetPixel(x, y, color);
                }
            }
            crosshairTexture.Apply();

            //set enemy color
            for (int y = 0; y < enemyTexture.height; y++)
            {
                for (int x = 0; x < enemyTexture.width; x++)
                {
                    Color color = (x & y) != 0 ? Color.red : Color.red;
                    enemyTexture.SetPixel(x, y, color);
                }
            }
            enemyTexture.Apply();

            //set neutral color
            for (int y = 0; y < neutralTexture.height; y++)
            {
                for (int x = 0; x < neutralTexture.width; x++)
                {
                    Color color = (x & y) != 0 ? Color.white : Color.white;
                    neutralTexture.SetPixel(x, y, color);
                }
            }
            neutralTexture.Apply();

            //set loot color
            for (int y = 0; y < lootTexture.height; y++)
            {
                for (int x = 0; x < lootTexture.width; x++)
                {
                    Color color = (x & y) != 0 ? Color.cyan : Color.cyan;
                    lootTexture.SetPixel(x, y, color);
                }
            }
            lootTexture.Apply();
        }
        void Update()
        {
            //Update periodically
            if (Time.frameCount % updateInterval == 0)
            {
                cachedEnemies = FindObjectsOfType<EntityEnemy>();
                cachedAnimals = FindObjectsOfType<EntityAnimal>();
                cachedNPCs = FindObjectsOfType<EntityNPC>();
            }

            if (Input.GetKeyDown(KeyCode.Insert))
            {
                //Change the boolean to it's counterpart state - i.e. from false to true and vice versa
                toggleWindow = !toggleWindow;
            }

            if (Input.GetKey(KeyCode.LeftAlt))
            {
                EntityEnemy targetEnemy = GetClosestEnemy(cachedEnemies);
                if (targetEnemy == null)
                {
                    EntityAnimal targetAnimal = GetClosestAnimal(cachedAnimals);
                    AutoAim(targetAnimal);
                }
                AutoAim(targetEnemy);
            }
        }
        void OnGUI()
        {
            if (cam != null)
            {
                DrawCrosshair();

                // Draw Enemy, Animal, and NPC esp
                DrawEntityESP(cachedEnemies);
                DrawEntityESP(cachedAnimals);
                DrawEntityESP(cachedNPCs);
            }

            if (toggleWindow)
            {
                windowRect = GUI.Window(0, windowRect, AssistWindowContent, "Assist Options");
            }

            if (showDebugMsg)
            {
                GUI.Label(new Rect(Screen.width/2, Screen.height/2, 750, 250), debugMsg);
            }
        }

        //Make contents of the window
        private void AssistWindowContent(int windowID)
        {
            if (GUI.Button(new Rect(10, 20, 100, 20), "Refresh Cam"))
            {
                cam = Camera.main;
                fpCam = FindObjectOfType<vp_FPCamera>();
            }

            if (GUI.Button(new Rect(10, 50, 100, 20), "Move Mouse"))
            {
                MoveMouse(Screen.width / 2, Screen.height / 2);
            }

            if (GUI.Button(new Rect(10, 80, 100, 20), "Show DebugMsg"))
            {
                showDebugMsg = !showDebugMsg;
            }

            aimSpeed = int.Parse(GUI.TextField(new Rect(10, 110, 100, 20), aimSpeed.ToString()));

            // Make the background the dragging area
            GUI.DragWindow();
        }

        private void DrawCrosshair()
        {
            Vector3 crosshairTarget = cam.transform.position + cam.transform.forward; //Calculate a point facing straight away from us
            Vector3 crosshairTarget_w2s = cam.WorldToScreenPoint(crosshairTarget); //Translate position to screen
            GUI.Label(new Rect(crosshairTarget_w2s.x, Screen.height - crosshairTarget_w2s.y, 10, 10), crosshairTexture);
        }

        private void DrawEntityESP(EntityEnemy[] entities)
        {
            if (entities != null && cam != null)
            {
                foreach (EntityEnemy entity in entities)
                {
                    if (entity != null)
                    {
                        float distanceToPlayer = Vector3.Distance(cam.transform.position, entity.transform.position);
                        if (entity.IsAlive() && distanceToPlayer <= espRange && Vector3.Angle(cam.transform.forward, entity.transform.position - cam.transform.position) < cam.fieldOfView)
                        {
                            Vector3 entityPOS_w2s = cam.WorldToScreenPoint(entity.transform.position);
                            Vector3 entityHeadPOS_w2s = cam.WorldToScreenPoint(entity.emodel.GetHeadTransform().position);
                            GUI.Label(new Rect(entityHeadPOS_w2s.x, Screen.height - entityHeadPOS_w2s.y, 10, 10), enemyTexture);
                            GUI.Label(new Rect(entityPOS_w2s.x, Screen.height - entityPOS_w2s.y, 500, 50), entity.EntityName + "\n" + Math.Round(distanceToPlayer).ToString());
                        }
                    }
                }
            }
        }

        private void DrawEntityESP(EntityAnimal[] entities)
        {
            if (entities != null && cam != null)
            {
                foreach (EntityAnimal entity in entities)
                {
                    if (entity != null)
                    {
                        float distanceToPlayer = Vector3.Distance(cam.transform.position, entity.transform.position);
                        if (entity.IsAlive() && distanceToPlayer <= espRange && Vector3.Angle(cam.transform.forward, entity.transform.position - cam.transform.position) < cam.fieldOfView)
                        {
                            Vector3 entityPOS_w2s = cam.WorldToScreenPoint(entity.transform.position);
                            Vector3 entityHeadPOS_w2s = cam.WorldToScreenPoint(entity.emodel.GetHeadTransform().position);
                            GUI.Label(new Rect(entityHeadPOS_w2s.x, Screen.height - entityHeadPOS_w2s.y, 10, 10), lootTexture);
                            GUI.Label(new Rect(entityPOS_w2s.x, Screen.height - entityPOS_w2s.y, 500, 50), entity.EntityName + "\n" + Math.Round(distanceToPlayer).ToString());
                        }
                    }
                }

            }
        }

        private void DrawEntityESP(EntityNPC[] entities)
        {
            if (entities != null && cam != null)
            {
                foreach (EntityNPC entity in entities)
                {
                    if (entity != null)
                    {
                        float distanceToPlayer = Vector3.Distance(cam.transform.position, entity.transform.position);
                        if (entity.IsAlive() && distanceToPlayer <= espRange && Vector3.Angle(cam.transform.forward, entity.transform.position - cam.transform.position) < cam.fieldOfView)
                        {
                            Vector3 entityPOS_w2s = cam.WorldToScreenPoint(entity.transform.position);
                            Vector3 entityHeadPOS_w2s = cam.WorldToScreenPoint(entity.emodel.GetHeadTransform().position);
                            GUI.Label(new Rect(entityHeadPOS_w2s.x, Screen.height - entityHeadPOS_w2s.y, 10, 10), neutralTexture);
                            GUI.Label(new Rect(entityPOS_w2s.x, Screen.height - entityPOS_w2s.y, 500, 50), entity.EntityName + "\n" + Math.Round(distanceToPlayer).ToString());
                        }
                    }
                }
            }
        }

        private void AutoAim(EntityEnemy target)
        {
            if (target != null)
            {
                Vector3 targetHead = target.emodel.GetHeadTransform().position;
                if (target.IsAlive() && Vector3.Distance(cam.transform.position, targetHead) <= espRange && Vector3.Angle(cam.transform.forward, targetHead - cam.transform.position) < cam.fieldOfView)
                {
                    Vector3 targetHeadPOS_w2s = cam.WorldToScreenPoint(targetHead);
                    if (new Regex("7 Days").IsMatch(Win32.GetActiveWindowTitle()))
                    {
                        MoveMouse((int)Math.Round(targetHeadPOS_w2s.x), (int)(Screen.height - Math.Round(targetHeadPOS_w2s.y)));
                        /*float yaw = CalculateYaw(target);
                        float pitch = CalculatePitch(target);
                        fpCam.Yaw = yaw;
                        fpCam.Pitch = pitch;
                        debugMsg = String.Format(
                            "N:{0}\nMe:{1}\nD:{2}\neX:{3}\neY:{4}\nT:{5}",
                            target.EntityName,
                            fpCam.transform.position.ToString(),
                            Math.Round(Vector3.Distance(cam.transform.position, targetHead)),
                            Math.Round(fpCam.Pitch),
                            Math.Round(fpCam.Yaw),
                            targetHead.ToString());*/
                    }
                }
                
            }
        }

        private void AutoAim(EntityAnimal target)
        {
            if (target != null)
            {
                Vector3 targetHead = target.emodel.GetHeadTransform().position;
                if (target.IsAlive() && Vector3.Distance(cam.transform.position, targetHead) <= espRange && Vector3.Angle(cam.transform.forward, targetHead - cam.transform.position) < cam.fieldOfView)
                {
                    Vector3 targetHeadPOS_w2s = cam.WorldToScreenPoint(targetHead);
                    if (new Regex("7 Days").IsMatch(Win32.GetActiveWindowTitle()))
                    {
                        MoveMouse((int)Math.Round(targetHeadPOS_w2s.x), (int)(Screen.height - Math.Round(targetHeadPOS_w2s.y)));
                        //fpCam.Yaw = CalculateYaw(target);
                        //fpCam.Pitch = CalculatePitch(target);
                    }
                }
            }
        }

        /*public Single CalculateYaw(EntityEnemy target)
        {
            Vector3 targetHead = target.emodel.GetHeadPosition();
            Vector3 targetToFace = targetHead - fpCam.transform.position;
            float yaw = Quaternion.LookRotation(targetToFace, fpCam.transform.up).eulerAngles.y;
            
            if (yaw > 180f)
            {
                yaw -= 360f;
            }
            return yaw;
        }*/

        /*public Single CalculatePitch(EntityEnemy target)
        {
            Vector3 targetHead = target.emodel.GetHeadPosition();
            Vector3 targetToFace = targetHead - fpCam.transform.position;
            float pitch = Quaternion.LookRotation(targetToFace, fpCam.transform.up).eulerAngles.x;

            if (pitch > 180f)
            {
                pitch -= 360f;
            }
            return pitch;
        }*/

        // Get closest enemy
        private EntityEnemy GetClosestEnemy(EntityEnemy[] entities)
        {
            EntityEnemy targetA = null;
            float targetADistance = 0;
            if (entities != null)
            {
                if (entities.Length == 1)
                {
                    return entities[0];
                }

                foreach (EntityEnemy entity in entities)
                {
                    if (entity != null)
                    {
                        float entityDistanceToPlayer = Vector3.Distance(cam.transform.position, entity.transform.position);
                        if (targetA == null && entity.IsAlive() && entityDistanceToPlayer <= aimRange)
                        {
                            targetA = entity;
                            targetADistance = entityDistanceToPlayer;
                            continue;
                        }
                        else if (entity.IsAlive() && entityDistanceToPlayer <= aimRange)
                        {
                            if (entityDistanceToPlayer < targetADistance)
                            {
                                targetA = entity;
                                targetADistance = entityDistanceToPlayer;
                            }
                        }
                    }
                }
                return targetA;
            }
            else
            {
                return null;
            }
        }

        private EntityAnimal GetClosestAnimal(EntityAnimal[] entities)
        {
            EntityAnimal targetA = null;
            float targetADistance = 0;
            if (entities != null)
            {
                if (entities.Length == 1)
                {
                    return entities[0];
                }

                foreach (EntityAnimal entity in entities)
                {
                    if (entity != null)
                    {
                        float entityDistanceToPlayer = Vector3.Distance(cam.transform.position, entity.transform.position);
                        if (targetA == null && entity.IsAlive() && entityDistanceToPlayer <= aimRange)
                        {
                            targetA = entity;
                            targetADistance = entityDistanceToPlayer;
                            continue;
                        }
                        else if (entity.IsAlive() && entityDistanceToPlayer <= aimRange)
                        {
                            if (entityDistanceToPlayer < targetADistance)
                            {
                                targetA = entity;
                                targetADistance = entityDistanceToPlayer;
                            }
                        }
                    }
                }
                return targetA;
            }
            else
            {
                return null;
            }
        }

        private void MoveMouse(int x, int y)
        {
            int dX = x - SysForms.Cursor.Position.X;
            int dY = y - SysForms.Cursor.Position.Y;
            Win32.Move(dX/aimSpeed, dY/aimSpeed);
        }
    }
}

public static class Win32
{
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

    [DllImport("User32.Dll")]
    public static extern long SetCursorPos(int x, int y);

    [DllImport("User32.Dll")]
    public static extern bool ClientToScreen(IntPtr hWnd, ref POINT point);

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int x;
        public int y;
    }

    [DllImport("user32.dll")]
    static extern void mouse_event(int dwFlags, int dx, int dy, int dwData, int dwExtraInfo);

    private const int MOUSEEVENTF_MOVE = 0x0001;
    private const int MOUSEEVENTF_ABSOLUTE = 0x8000;

    public static void Move(int xDelta, int yDelta)
    {
        mouse_event(MOUSEEVENTF_MOVE, xDelta, yDelta, 0, 0);      
    }

    public static string GetActiveWindowTitle()
    {
        const int nChars = 256;
        StringBuilder Buff = new StringBuilder(nChars);
        IntPtr handle = GetForegroundWindow();

        if (GetWindowText(handle, Buff, nChars) > 0)
        {
            return Buff.ToString();
        }
        return null;
    }
}