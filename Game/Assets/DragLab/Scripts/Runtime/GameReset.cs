using System;
using DragLab.Domain;
using System.Collections.Generic;
using UnityEngine;
using DragLab.Infrastructure;

namespace DragLab.Runtime
{
    public sealed partial class PrototypeGame
    {
        bool resetGameConfirm;
        long resetGameRevision;
        internal readonly Dictionary<string, Rect> resetRects = new Dictionary<string, Rect>();
        internal void ShowDisplayUnitsForInspection() { OpenAudioSettings(); scroll.y = 10000; }
        bool ResetButton(string id, string label)
        {
            bool clicked = GUILayout.Button(label, GUILayout.Height(32));
            if (Event.current.type == EventType.Repaint)
            {
                var rect = GUILayoutUtility.GetLastRect();
                resetRects[id] = new Rect(GUIUtility.GUIToScreenPoint(rect.position), rect.size * GUI.matrix.m00);
            }
            return clicked;
        }
        void SettingsGUI()
        {
            AudioSettingsView.Draw(Audio);
            GUILayout.Space(16);
            ResetSettingsGUI();
            GUILayout.Space(12);
            if (SimulationSettingsView.Draw(settings)) saveDelay = .5f;
            GUILayout.Space(12); UnitSettingsView.Draw();
        }
        void ResetSettingsGUI()
        {
            GUILayout.Label("RESET GAME", stat);
            GUILayout.Label("Start again with " + DisplayUnits.Money(1200) + " and a stock starter car.");
            GUILayout.Label("Keeps driving assists, AI difficulty and volume settings.");
            if (!resetGameConfirm)
            {
                if (ResetButton("request", "Reset game..."))
                { resetGameRevision = profile.revision; resetGameConfirm = true; resetRects.Clear(); }
                return;
            }
            GUILayout.Label("Clear cash, career wins, garage income, cars, inventory, tunes, paint and records?");
            GUILayout.BeginHorizontal();
            if (ResetButton("cancel", "Cancel")) { resetGameConfirm = false; resetRects.Clear(); }
            if (ResetButton("confirm", "Reset progression"))
            {
                long revision = resetGameRevision;
                pendingAction = () => CommitGameReset(revision);
            }
            GUILayout.EndHorizontal();
        }
        internal bool CommitGameReset(long expectedRevision)
        {
            if (screen != "Settings" || !resetGameConfirm || profile.revision != expectedRevision)
            { resetGameConfirm = false; notice = "Reset cancelled; reopen Settings and try again."; return false; }
            // Publish only after both the new profile and its recovery backup are saved.
            if (!ProfileStore.TryReset(profile, out var fresh))
            { resetGameConfirm = false; notice = ProfileStore.Status; return false; }
            profile = fresh; settings = profile.driving; difficulty = profile.difficulty;
            DiscardPreviews();
            saveDelay = 0; pendingAction = null;
            race?.CloseOpponentWorker(); race = null; activeEvent = null; previousPass = finishedPass = null;
            benchmark = null; lastBenchmarkKey = null; accumulator = 0;
            practiceRun = awarded = paused = audioSettingsOpen = false;
            cart.Clear();cartOwner=null;previewCart=false;
            freeDistance = 402.336f; freeOpponent = 0; partCategory = 0;
            resetGameConfirm = false; resetRects.Clear(); partRects.Clear();
            scroll = resultScroll = Vector2.zero; controls.Clear();
            screen = "Settings"; ShowGarageCar(); notice = ProfileStore.Status;
            PhysicalBenchTransfer.Take();
            return true;
        }
    }
}
