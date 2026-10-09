using System.Collections.Generic;
using UnityEngine;
using DragLab.Domain;

namespace DragLab.Runtime
{
    // The overlay and hit testing share rectangles. Each finger captures its own control.
    public sealed class RaceInputAdapter
    {
        public const int GasControl = 0, ClutchControl = 1, DownControl = 2, UpControl = 3, PauseControl = 4, BrakeControl = 5, ControlCount = 6;
        struct Contact { public int control; public float value; }
        readonly Dictionary<int, Contact> contacts = new Dictionary<int, Contact>();
        double keyboardGas, keyboardClutch, keyboardBrake;
        bool launch, up, down, pause, blockPointers;
        public static float Scale(int width, int height) { return Mathf.Max(.65f, Mathf.Min(width / 1100f, height / 700f)); }
        public static Rect ControlRect(int index, float width, float height)
        {
            float cell = (width - 32) / ControlCount;
            return new Rect(16 + index * cell, height - 91, cell - 4, 64);
        }
        public void Clear()
        {
            contacts.Clear(); launch = up = down = pause = false; keyboardGas = keyboardClutch = keyboardBrake = 0; blockPointers = true;
        }
        public void Poll()
        {
            SubmitKeyboard(Input.GetKey(KeyCode.W), Input.GetKey(KeyCode.Space), Input.GetKey(KeyCode.LeftShift), Input.GetKey(KeyCode.S),
                Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter),
                Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.UpArrow), Input.GetKeyDown(KeyCode.Q) || Input.GetKeyDown(KeyCode.DownArrow));
            // Ignore the click that entered/resumed a race, without suppressing held keyboard pedals.
            if (blockPointers)
            {
                if (Input.GetMouseButton(0) || Input.touchCount > 0) return;
                blockPointers = false;
            }
            float scale = Scale(Screen.width, Screen.height);
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);
                var position = new Vector2(touch.position.x, Screen.height - touch.position.y) / scale;
                SubmitPointer(touch.fingerId, position, touch.phase == TouchPhase.Began,
                    touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled, Screen.width / scale, Screen.height / scale);
            }
            // Touch already owns its pointer; ignore the emulated mouse on mobile.
            if (Input.touchCount == 0)
            {
                var mouse = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y) / scale;
                SubmitPointer(-1, mouse, Input.GetMouseButtonDown(0), !Input.GetMouseButton(0), Screen.width / scale, Screen.height / scale);
            }
        }
        public void BeginFrame(double gas, double clutch, double brake = 0) { keyboardGas = gas; keyboardClutch = clutch; keyboardBrake = brake; }
        public void SubmitKeyboard(bool w, bool space, bool clutch, bool brake, bool launchPressed, bool upPressed, bool downPressed)
        {
            BeginFrame(w || space ? 1 : 0, clutch ? 1 : 0, brake ? 1 : 0);
            launch |= launchPressed; up |= upPressed; down |= downPressed;
        }
        public void SubmitPointer(int id, Vector2 point, bool began, bool ended, float width, float height)
        {
            if (ended) { contacts.Remove(id); return; }
            if (began)
                for (int i = 0; i < ControlCount; i++)
                    if (ControlRect(i, width, height).Contains(point))
                    {
                        contacts[id] = new Contact { control = i };
                        if (i == DownControl) down = true; if (i == UpControl) up = true; if (i == PauseControl) pause = true;
                        break;
                    }
            if (contacts.TryGetValue(id, out Contact contact))
            {
                Rect rect = ControlRect(contact.control, width, height);
                // Middle/right is full pedal; slide toward the left edge for partial input.
                contact.value = point.x >= rect.center.x ? 1 : Mathf.Clamp01((point.x - rect.x) / (rect.width * .5f)); contacts[id] = contact;
            }
        }
        public DriverInput Peek()
        {
            var input = new DriverInput { throttle = keyboardGas, clutch = keyboardClutch, brake = keyboardBrake, launch = launch, shiftUp = up, shiftDown = down };
            foreach (var contact in contacts.Values)
            {
                if (contact.control == GasControl) input.throttle = System.Math.Max(input.throttle, contact.value);
                if (contact.control == ClutchControl) input.clutch = System.Math.Max(input.clutch, contact.value);
                if (contact.control == BrakeControl) input.brake = System.Math.Max(input.brake, contact.value);
            }
            return input;
        }
        public DriverInput Consume() { var input = Peek(); launch = up = down = false; return input; }
        public bool ConsumePause() { bool value = pause; pause = false; return value; }
    }
}
