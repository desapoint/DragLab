using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace DragLab.Runtime
{
    // Shared native menu presentation; settings edits remain isolated until Apply.
    internal sealed partial class NativeMenuShell : MonoBehaviour
    {
        static readonly string[] routes = { "Garage", "Parts", "Tuning", "Race setup", "Business", "Stats" };
        static readonly string[] names = { "Garage", "Build", "Tuning", "Race", "Business", "Data" };
        static readonly string[] categories = { "Difficulty / Driving", "Sound", "Units", "Controls", "General" };
        static readonly string[] categoryIds = { "Difficulty", "Sound", "Units", "Controls", "General" };
        PrototypeGame game;
        UIDocument document;
        PanelSettings panel;
        VisualElement root;
        readonly List<RacingSurface> surfaces = new List<RacingSurface>();
        int category;
        string renderedRoute;
        internal VisualElement Root => root;
        internal PanelSettings Panel => panel;
        internal int Category => category;
        internal bool Visible => root != null && !root.ClassListContains("hidden");
        internal static bool Supports(string route) => Array.IndexOf(routes, route) >= 0 || route == "Settings";
        internal static float DisplayScale(int width, int height) => Mathf.Max(1, Mathf.Min(width / 1920f, height / 1080f));

        internal void Initialize(PrototypeGame owner)
        {
            game = owner;
            panel = Instantiate(Resources.Load<PanelSettings>("UI/NativePanel"));
            var host = new GameObject("Native menu document"); host.transform.SetParent(transform);
            document = host.AddComponent<UIDocument>();
            document.panelSettings = panel;
            document.visualTreeAsset = Resources.Load<VisualTreeAsset>("UI/NativeShell");
            root = document.rootVisualElement.Q("shell");
            root.AddToClassList("hidden");
            root.RegisterCallback<KeyDownEvent>(evt => {
                if (evt.keyCode != KeyCode.Tab) return;
                root.focusController.IgnoreEvent(evt); evt.StopImmediatePropagation();
            }, TrickleDown.TrickleDown);
            root.RegisterCallback<NavigationMoveEvent>(evt => {
                if (evt.direction != NavigationMoveEvent.Direction.Next && evt.direction != NavigationMoveEvent.Direction.Previous) return;
                root.focusController.IgnoreEvent(evt); evt.StopImmediatePropagation();
            }, TrickleDown.TrickleDown);
            root.RegisterCallback<CustomStyleResolvedEvent>(evt => {
                if(evt.target!=root)return;
                foreach(var surface in surfaces)surface.MarkDirtyRepaint();
                foreach(var surface in switchSurfaces)surface.MarkDirtyRepaint();
                root.Query<SliderFill>().ForEach(fill=>fill.MarkDirtyRepaint());
            });
            AddSurface(root.Q("metalBackground"), RacingSurface.Kind.Metal);
            AddSurface(root.Q("navBar"), RacingSurface.Kind.Header);
            AddSurface(root.Q("footerBar"), RacingSurface.Kind.Footer);
            for (int i = 0; i < routes.Length; i++)
            {
                int index = i;
                var button = root.Q<Button>("nav" + names[i]);
                button.text = "";
                button.Insert(0, new FilledMenuIcon(names[i]));
                button.Add(new Label(names[i].ToUpperInvariant()) { pickingMode = PickingMode.Ignore });
                AddSurface(button, RacingSurface.Kind.Navigation);
                button.clicked += () => Navigate(routes[index]);
                Hint(button, names[i]);
            }
            var settingsButton = root.Q<Button>("settingsButton");
            settingsButton.Add(new FilledMenuIcon("Settings"));
            AddSurface(settingsButton, RacingSurface.Kind.Primary);
            settingsButton.clicked += () => Navigate("Settings");
            Hint(settingsButton, "Settings");
            foreach (string resource in new[] { "cash", "fuel", "rep" })
                root.Q(resource + "Group").Insert(0, new FilledMenuIcon(resource));
            for (int i = 0; i < categoryIds.Length; i++)
            {
                int index = i;
                var tab = root.Q<Button>("tab" + categoryIds[i]);
                string label = tab.text;
                tab.text = "";
                tab.Add(new Label(label) { pickingMode = PickingMode.Ignore });
                AddSurface(tab, i == 0 ? RacingSurface.Kind.FirstTab : RacingSurface.Kind.Tab);
                tab.clicked += () => { category = index; renderedRoute = null; Refresh(); ShowCategory(); };
                Hint(tab, categories[i]);
            }
            InitializeOwnedCars();
            InitializeSettings();
            ExcludeTabTraversal();
        }

        void ExcludeTabTraversal()
        {
            root.tabIndex = -1;
            root.Query<VisualElement>().ForEach(element => element.tabIndex = -1);
        }

        void AddSurface(VisualElement owner, RacingSurface.Kind kind)
        {
            // Button's immediate text draws before child decoration. Retain its
            // caption as a child above the surface, including generated controls.
            if (owner is Button button && !string.IsNullOrEmpty(button.text))
            { string caption = button.text; button.text = ""; button.Add(new Label(caption) { pickingMode = PickingMode.Ignore }); }
            var surface = new RacingSurface(owner, kind); owner.Insert(0, surface); surfaces.Add(surface);
        }

        void Hint(Button button, string text)
        {
            button.tooltip = text;
        }
        void LateUpdate()
        {
            Refresh();
            if (Visible && Input.GetKeyDown(KeyCode.Escape)) { if (ModalOpen) CloseDialog(); else if (OwnedDetailsOpen) BackToOwnedCars(); }
            foreach (var surface in surfaces) surface.Tick(Visible);
            foreach (var surface in switchSurfaces) surface.Tick(Visible);
        }
        internal void Refresh()
        {
            if (root == null || game.Profile == null) return;
            string route = game.ScreenName;
            SyncSettingsEntry(route);
            bool show = Supports(route) && InterfaceComparison.IsReplacement(route);
            root.EnableInClassList("hidden", !show);
            if (!show) return;
            int width = panel.targetTexture == null ? Screen.width : panel.targetTexture.width;
            int height = panel.targetTexture == null ? Screen.height : panel.targetTexture.height;
            panel.scale = DisplayScale(width, height);
            root.EnableInClassList("compact", width / panel.scale < 1150);
            var profile = game.Profile; var car = profile.SelectedCar;
            SetText("cash", "$" + profile.cash.ToString("N0"));
            SetText("fuel", car == null ? "— L" : car.fuel.litres.ToString("F1") + " / " + profile.catalog.Spec(car.vehicleId).fuel.capacityLitres.ToString("F0") + " L");
            SetText("progression", "REP " + profile.reputation);
            RefreshOwnedCars(route);
            if (renderedRoute == route) return;
            renderedRoute = route;
            bool settings = route == "Settings";
            for (int i = 0; i < routes.Length; i++) root.Q<Button>("nav" + names[i]).EnableInClassList("selected", route == routes[i]);
            root.Q<Button>("settingsButton").EnableInClassList("selected", settings);
            root.Q("settingsTabs").EnableInClassList("hidden", !settings);
            root.Q("settingsBody").EnableInClassList("hidden", !settings);
            root.Q("settingsActions").EnableInClassList("hidden", !settings);
            ShowCategory();
            for (int i = 0; i < categoryIds.Length; i++) root.Q<Button>("tab" + categoryIds[i]).EnableInClassList("selected", i == category);
        }
        void SetText(string id, string value) { var label = root.Q<Label>(id); if (label.text != value) label.text = value; }
        void OnDestroy() { ownedCancellation?.Cancel(); ownedCancellation?.Dispose(); if (panel != null) Destroy(panel); }
    }

    // Filled, procedurally tessellated shapes stay sharp at native display resolution.
    internal sealed class FilledMenuIcon : VisualElement
    {
        readonly string kind;
        internal FilledMenuIcon(string name)
        { kind = name; pickingMode = PickingMode.Ignore; AddToClassList("filled-icon"); generateVisualContent += Draw; }
        void Draw(MeshGenerationContext context)
        {
            if (contentRect.width <= 0 || contentRect.height <= 0) return;
            var p = context.painter2D; p.fillColor = new Color(.9f, .9f, .93f);
            float sx = contentRect.width / 24, sy = contentRect.height / 24;
            float yOffset = kind == "Race" || kind == "fuel" ? -.5f : 0;
            Action<float[]> polygon = points => {
                p.BeginPath(); p.MoveTo(new Vector2(points[0] * sx, (points[1]+yOffset) * sy));
                for (int i = 2; i < points.Length; i += 2) p.LineTo(new Vector2(points[i] * sx, (points[i + 1]+yOffset) * sy));
                p.ClosePath(); p.Fill();
            };
            Action<float,float,float,float> rect = (x,y,w,h) => polygon(new[] { x,y,x+w,y,x+w,y+h,x,y+h });
            Action<float,float,float> circle = (x,y,r) => { p.BeginPath(); p.Arc(new Vector2(x*sx,y*sy), r*sx, Angle.Degrees(0), Angle.Degrees(360)); p.ClosePath(); p.Fill(); };
            switch (kind)
            {
                case "Expand": polygon(new float[] {4,8,20,8,12,17}); break;
                case "Back": polygon(new float[] {2,12,11,3,11,8,22,8,22,16,11,16,11,21}); break;
                case "Pencil": polygon(new float[] {4,15,9,20,20,9,15,4}); polygon(new float[] {3,17,7,21,2,22}); polygon(new float[] {16,3,19,1,23,5,21,8}); break;
                case "Settings":
                    var gear = new float[64];
                    for (int i = 0; i < 32; i++) { float a = i * Mathf.PI / 16; float r = i % 4 < 2 ? 10 : 7.8f; gear[i*2] = 12 + Mathf.Cos(a)*r; gear[i*2+1] = 12 + Mathf.Sin(a)*r; }
                    polygon(gear);
                    p.strokeColor = new Color(1,1,1,.65f); p.lineWidth = .65f; p.Stroke();
                    p.fillColor = new Color(.15f,.15f,.17f); circle(12,12,3.3f);
                    p.strokeColor = new Color(1,1,1,.6f); p.lineWidth = .7f;
                    p.BeginPath(); p.Arc(new Vector2(12*sx,12*sy),4*sx,Angle.Degrees(195),Angle.Degrees(330)); p.Stroke(); break;
                case "Garage": polygon(new float[] {2,10,12,2,22,10,22,22,16,22,16,13,8,13,8,22,2,22}); break;
                case "Build": polygon(new float[] {4,2,9,2,9,7,13,7,13,2,18,2,18,9,14,13,14,22,8,22,8,13,4,9}); break;
                case "Tuning": rect(3,2,3,20); rect(10,2,3,20); rect(17,2,3,20); rect(1,6,7,4); rect(8,14,7,4); rect(15,8,7,4); break;
                case "Race": rect(3,2,3,21); rect(6,2,15,13); p.fillColor = new Color(.15f,.15f,.17f); rect(6,2,5,4); rect(16,2,5,4); rect(11,6,5,5); rect(6,11,5,4); rect(16,11,5,4); break;
                case "Business": rect(2,7,20,15); rect(7,2,10,4); break;
                case "Data": rect(2,14,5,8); rect(10,8,5,14); rect(18,2,4,20); break;
                case "cash": rect(2,5,20,14); p.fillColor = new Color(.15f,.15f,.17f); circle(12,12,4); break;
                case "fuel": rect(3,3,12,19); rect(16,8,3,13); rect(15,19,6,3); rect(19,5,3,15); p.fillColor = new Color(.15f,.15f,.17f); rect(5,5,8,6); break;
                case "rep": polygon(new float[] {12,1,15,8,23,9,17,14,19,23,12,18,5,23,7,14,1,9,9,8}); break;
            }
        }
    }
}
