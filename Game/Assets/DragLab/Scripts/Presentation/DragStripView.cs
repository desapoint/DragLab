using System.Collections.Generic;
using UnityEngine;
using DragLab.Domain;

namespace DragLab.Presentation
{
    public sealed class DragStripView : MonoBehaviour
    {
        const float MetersToView = .18f;
        struct Tile { public Transform transform; public Vector3 origin; }
        readonly List<Tile> tiles = new List<Tile>();
        readonly List<SpriteRenderer> lamps = new List<SpriteRenderer>();
        Transform tree, finish;
        Sprite off, amber, green, red;
        public void Build()
        {
            for (int i = 0; i < 32; i++)
            {
                AddTile("asphalt-tile", new Vector3(i * 1.28f, -1.4f), -50, new Vector3(1, 5, 1));
                AddTile("barrier-tile", new Vector3(i * 1.28f, .8f), -40, Vector3.one);
                AddTile("lane-stripe", new Vector3(i * 1.28f, -1.3f), -30, Vector3.one * .8f);
            }
            tree = Sprite("tree-frame", new Vector3(-3, 1.3f), 2000).transform;
            for (int i = 0; i < 5; i++)
            {
                var lamp = Sprite("bulb-off", Vector3.zero, 2001);
                lamp.transform.SetParent(tree, false);
                lamp.transform.localPosition = new Vector3(0, .47f - i * .24f);
                lamps.Add(lamp);
            }
            off = Load("bulb-off"); amber = Load("bulb-amber"); green = Load("bulb-green"); red = Load("bulb-red");
            finish = Sprite("finish-checker", new Vector3(70, -1.4f), -20).transform;
            finish.localScale = new Vector3(1, 5, 1);
            ShowGarage();
        }
        void AddTile(string file, Vector3 origin, int order, Vector3 scale)
        {
            var t = Sprite(file, origin, order).transform;
            t.localScale = scale; tiles.Add(new Tile { transform = t, origin = origin });
        }
        Sprite Load(string name) { return Resources.Load<Sprite>("Sprites/Track/" + name); }
        SpriteRenderer Sprite(string file, Vector3 position, int order)
        {
            var obj = new GameObject(file); obj.transform.SetParent(transform, false); obj.transform.localPosition = position;
            var r = obj.AddComponent<SpriteRenderer>(); r.sprite = Load(file); r.sortingOrder = order; return r;
        }
        public void ShowGarage()
        {
            tree.gameObject.SetActive(false); finish.gameObject.SetActive(false);
            foreach (var tile in tiles) tile.transform.localPosition = new Vector3(Mathf.Repeat(tile.origin.x + 12, 40.96f) - 12, tile.origin.y, 0);
        }
        public void Render(RaceSimulation race)
        {
            tree.gameObject.SetActive(true); finish.gameObject.SetActive(true);
            float travel = (float)race.player.state.distance * MetersToView;
            foreach (var tile in tiles)
                tile.transform.localPosition = new Vector3(Mathf.Repeat(tile.origin.x - travel + 12, 40.96f) - 12, tile.origin.y, 0);
            tree.localPosition = new Vector3(-3 - travel, 1.3f);
            finish.localPosition = new Vector3(2 + (float)race.distance * MetersToView - travel, -1.4f);
            for (int i = 0; i < 5; i++)
            {
                bool lit = i < 3 ? race.time >= 1.5 + i * .5 && race.time < 3 : i == 3 ? race.time >= 3 && !race.player.state.disqualified : race.player.state.disqualified;
                lamps[i].sprite = lit ? i < 3 ? amber : i == 3 ? green : red : off;
            }
        }
    }
}
