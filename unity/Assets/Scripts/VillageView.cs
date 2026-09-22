using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LittleGeorgies
{
    public sealed class VillageView : MonoBehaviour
    {
        public static readonly Vector2 Store = new Vector2(0, 0.1f);
        public static readonly Vector2 Orchard = new Vector2(-9.4f, 0.7f);
        public static readonly Vector2 Workshop = new Vector2(10.5f, -0.3f);
        public static readonly Vector2 Homes = new Vector2(0.4f, 4.4f);
        public static readonly Vector2 Rest = new Vector2(-9.8f, -3.1f);
        public static readonly Vector2 Junction = new Vector2(0, -1.1f);
        public static readonly Vector2 Camp = new Vector2(-4.5f, -1.2f);
        public Camera Camera { get; private set; }
        public readonly Dictionary<int, GeorgieActor> Actors = new Dictionary<int, GeorgieActor>();
        Sprite[] frames;
        Material spriteMaterial;
        SpriteRenderer background;
        Sprite houseSprite;
        SpriteRenderer workbench;
        GameObject commonLabel, workshopLabel, homesLabel;
        readonly List<SpriteRenderer> houses = new List<SpriteRenderer>();
        public int VisibleHomes => houses.Count(h => h.gameObject.activeSelf);
        public bool WorkshopVisible => workbench.gameObject.activeSelf;
        Font font;

        public void Initialize()
        {
            Camera = new GameObject("Village camera").AddComponent<Camera>();
            Camera.transform.SetParent(transform, false);
            Camera.orthographic = true;
            Camera.transform.position = new Vector3(0, 0, -10);
            Camera.clearFlags = CameraClearFlags.SolidColor;
            Camera.backgroundColor = new Color(0.55f, 0.69f, 0.62f);
            Camera.tag = "MainCamera";
            var texture = Resources.Load<Texture2D>("Art/OrchardField");
            background = new GameObject("Painted settlement").AddComponent<SpriteRenderer>();
            background.transform.SetParent(transform, false);
            background.sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.one * .5f, texture.width / 32f);
            background.sortingOrder = -100;
            var sheet = Resources.Load<Texture2D>("Art/GeorgieSheet");
            spriteMaterial = new Material(Resources.Load<Shader>("Art/GeorgieSprite"));
            var buildings = Resources.Load<Texture2D>("Art/BuildingSheet");
            int buildingWidth = buildings.width / 2;
            var benchSprite = Sprite.Create(buildings, new Rect(0, 0, buildingWidth, buildings.height), new Vector2(.5f, .22f), 220);
            houseSprite = Sprite.Create(buildings, new Rect(buildingWidth, 0, buildingWidth, buildings.height), new Vector2(.5f, .22f), 210);
            workbench = Structure("Basket workbench", benchSprite, Workshop + new Vector2(0, .7f));
            workbench.gameObject.SetActive(false);
            frames = new Sprite[8];
            int width = sheet.width / 4, height = sheet.height / 2;
            for (int i = 0; i < 8; i++)
                frames[i] = Sprite.Create(sheet, new Rect(i % 4 * width, (1 - i / 4) * height, width, height), new Vector2(.5f, .045f), 256);
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            SiteLabel("ORCHARD", Orchard + new Vector2(0, 3.5f));
            commonLabel = SiteLabel("COMMON APPLES", Store + new Vector2(0, 3.2f));
            workshopLabel = SiteLabel("BASKET WEAVING", Workshop + new Vector2(0, -.7f));
            homesLabel = SiteLabel("HOMES", Homes + new Vector2(0, -1.7f));
            commonLabel.SetActive(false);
            workshopLabel.SetActive(false);
            homesLabel.SetActive(false);
        }

        SpriteRenderer Structure(string name, Sprite sprite, Vector2 position)
        {
            var renderer = new GameObject(name).AddComponent<SpriteRenderer>();
            renderer.transform.SetParent(transform, false);
            renderer.transform.position = position;
            renderer.sprite = sprite;
            renderer.sharedMaterial = spriteMaterial;
            renderer.sortingOrder = 600 - Mathf.RoundToInt(position.y * 25);
            return renderer;
        }

        public static Vector2 HomePosition(int index) => new Vector2(-5.4f + (index % 4) * 3.6f, 3.4f - (index / 4) * 2.1f);

        GameObject SiteLabel(string title, Vector2 position)
        {
            var group = new GameObject(title + " site");
            group.transform.SetParent(transform, false);
            group.transform.position = position;
            var label = new GameObject(title).AddComponent<TextMesh>();
            label.transform.SetParent(group.transform, false);
            label.transform.localPosition = Vector3.zero;
            label.font = font;
            label.fontSize = 48;
            label.characterSize = .08f;
            label.anchor = TextAnchor.MiddleCenter;
            label.color = new Color(.13f, .22f, .17f);
            label.text = title;
            var renderer = label.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = font.material;
            renderer.sortingOrder = 1800;
            var shadow = Instantiate(label, label.transform.parent);
            shadow.name = title + " shadow";
            shadow.transform.position += new Vector3(.035f, -.035f, 0);
            shadow.color = new Color(.06f, .10f, .07f, .9f);
            shadow.GetComponent<MeshRenderer>().sortingOrder = 1799;
            label.color = Color.white;
            return group;
        }

        public void Sync(Society society)
        {
            foreach (var g in society.People)
            {
                if (Actors.ContainsKey(g.Id)) continue;
                var actor = new GameObject(g.Title).AddComponent<GeorgieActor>();
                actor.transform.SetParent(transform, false);
                actor.Initialize(g, frames, spriteMaterial, font);
                actor.transform.position = Camp + ClusterOffset(society.People.IndexOf(g), society.People.Count);
                Actors.Add(g.Id, actor);
            }
        }

        public static Vector2 Offset(int id) => new Vector2(((id - 1) % 4 - 1.5f) * 1.75f, ((id - 1) / 4) * -1.65f);

        static Vector2 ClusterOffset(int index, int count)
        {
            int columns = Mathf.Min(4, count);
            int row = index / columns;
            int rowCount = Mathf.Min(columns, count - row * columns);
            return new Vector2((index % columns - (rowCount - 1) * .5f) * 1.75f, row * -1.65f);
        }

        public void Tick(Society society, float progress, float elapsed, float dt, int selected)
        {
            Camera.orthographicSize = Mathf.Max(9, 16 / Camera.aspect);
            background.color = Color.Lerp(Color.white, new Color(.82f, .85f, 1), Mathf.SmoothStep(0, .32f, Mathf.Max(0, (progress - .78f) / .22f)));
            workbench.gameObject.SetActive(society.Specialist);
            commonLabel.SetActive(society.Specialist);
            workshopLabel.SetActive(society.Specialist);
            int visibleHomes = Mathf.Min(8, society.TotalHomes);
            while (houses.Count < visibleHomes)
                houses.Add(Structure("Built home " + (houses.Count + 1), houseSprite, HomePosition(houses.Count)));
            for (int i = 0; i < houses.Count; i++) houses[i].gameObject.SetActive(i < visibleHomes);
            homesLabel.SetActive(visibleHomes > 0);
            if (visibleHomes > 0) homesLabel.transform.position = HomePosition(0) + new Vector2(0, -.45f);
            var housed = society.People.Where(p => p.House).ToList();
            foreach (var g in society.People)
            {
                Vector2 destination;
                string activity;
                bool carrying = false;
                if (progress < .54f)
                {
                    destination = g.Job == Job.Harvest ? Orchard : g.Job == Job.Rest ? (g.House ? HomePosition(housed.IndexOf(g)) : Rest)
                        : g.Job == Job.Administer ? Store : g.Job == Job.Houses ? HomePosition(Mathf.Min(7, society.TotalHomes)) : Workshop;
                    activity = g.Job == Job.Harvest ? "Gathering" : g.Job == Job.Rest ? (g.House ? "Resting at home" : "Resting outdoors")
                        : g.Job == Job.Houses ? "Building" : g.Job == Job.Baskets ? "Weaving" : "Administering";
                }
                else if (progress < .75f)
                {
                    destination = g.Job == Job.Rest ? (g.House ? HomePosition(housed.IndexOf(g)) : Rest) : society.Specialist ? Store : Camp;
                    carrying = g.Job == Job.Harvest || g.Job == Job.Baskets;
                    activity = carrying ? (g.Job == Job.Harvest ? $"Carrying {g.Yield} apples" : "Carrying baskets")
                        : g.Job == Job.Rest ? (g.House ? "Resting at home" : "Resting outdoors") : "Sharing apples";
                }
                else if (progress < .9f)
                {
                    destination = society.Specialist ? Store : Camp;
                    activity = "Eating apples";
                }
                else
                {
                    destination = g.House ? HomePosition(housed.IndexOf(g)) : Rest;
                    activity = g.Mood == Mood.Broken ? "Hungry" : g.House ? "Sleeping at home" : "Sleeping outdoors";
                }
                var neighbors = progress < .54f ? society.People.Where(p => p.Job == g.Job).ToList() : society.People;
                bool atOwnHome = g.House && (progress >= .9f || (progress < .75f && g.Job == Job.Rest));
                Actors[g.Id].Tick(destination + (atOwnHome ? Vector2.zero : ClusterOffset(neighbors.IndexOf(g), neighbors.Count)), activity, carrying, elapsed, dt, g.Id == selected);
            }
        }
    }

    public sealed class GeorgieActor : MonoBehaviour
    {
        public Georgie Person { get; private set; }
        public string Activity { get; private set; }
        public float DistanceWalked { get; private set; }
        public int FrameChanges { get; private set; }
        SpriteRenderer body;
        SpriteRenderer marker;
        TextMesh label;
        TextMesh cargo;
        Sprite[] frames;
        readonly Queue<Vector2> route = new Queue<Vector2>();
        Vector2 lastDestination = Vector2.one * 999;
        int lastFrame = -1;

        public void Initialize(Georgie person, Sprite[] animationFrames, Material material, Font font)
        {
            Person = person;
            frames = animationFrames;
            body = new GameObject("Animated Georgie").AddComponent<SpriteRenderer>();
            body.transform.SetParent(transform, false);
            body.sharedMaterial = material;
            body.sprite = frames[4];
            body.transform.localScale = Vector3.one * 1.22f;
            var collider = gameObject.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(1.3f, 2.3f);
            collider.offset = new Vector2(0, 1.1f);
            marker = new GameObject("Role and selection marker").AddComponent<SpriteRenderer>();
            marker.transform.SetParent(transform, false);
            var dot = new Texture2D(32, 32);
            var pixels = new Color[32 * 32];
            for (int y = 0; y < 32; y++)
                for (int x = 0; x < 32; x++)
                    pixels[y * 32 + x] = Vector2.Distance(new Vector2(x, y), new Vector2(15.5f, 15.5f)) < 15 ? Color.white : Color.clear;
            dot.SetPixels(pixels); dot.Apply();
            marker.sprite = Sprite.Create(dot, new Rect(0, 0, 32, 32), Vector2.one * .5f, 32);
            marker.transform.localScale = new Vector3(1.35f, .3f, 1);
            label = Text("Name", font, new Vector3(0, 2.55f, 0), .12f);
            cargo = Text("Carried goods", font, new Vector3(.8f, 1.1f, 0), .13f);
        }

        TextMesh Text(string name, Font font, Vector3 position, float size)
        {
            var t = new GameObject(name).AddComponent<TextMesh>();
            t.transform.SetParent(transform, false);
            t.transform.localPosition = position;
            t.font = font; t.fontSize = 48; t.characterSize = size;
            t.anchor = TextAnchor.MiddleCenter;
            t.color = new Color(.10f, .16f, .11f);
            t.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            t.GetComponent<MeshRenderer>().sortingOrder = 1900;
            return t;
        }

        public void Tick(Vector2 destination, string activity, bool carrying, float elapsed, float dt, bool selected)
        {
            if (Vector2.Distance(destination, lastDestination) > .1f)
            {
                route.Clear();
                // This illustrated scene has fixed paths: route via the clearing between work sites.
                if (Vector2.Distance(transform.position, destination) > 7)
                    route.Enqueue(VillageView.Junction + VillageView.Offset(Person.Id));
                route.Enqueue(destination);
                lastDestination = destination;
            }
            var before = transform.position;
            if (route.Count > 0 && dt > 0)
            {
                transform.position = Vector2.MoveTowards(transform.position, route.Peek(), dt * (Person.Mood == Mood.Broken ? 2.7f : 3.6f));
                if (Vector2.Distance(transform.position, route.Peek()) < .02f) route.Dequeue();
            }
            bool moving = route.Count > 0;
            DistanceWalked += Vector3.Distance(before, transform.position);
            Activity = moving ? "Walking - " + activity.ToLowerInvariant() : activity;
            if (Mathf.Abs(transform.position.x - before.x) > .001f) body.flipX = transform.position.x < before.x;
            int frame = moving ? (int)(elapsed * 7 + Person.Id) % 4
                : activity.StartsWith("Resting") || activity.StartsWith("Sleeping") ? 7
                : Person.Mood == Mood.Broken ? 6
                : activity == "Gathering" || activity == "Building" || activity == "Weaving" ? ((int)(elapsed * 2) % 2 == 0 ? 5 : 4)
                : Person.Mood == Mood.Tired ? 6 : 4;
            if (frame != lastFrame) { FrameChanges++; lastFrame = frame; body.sprite = frames[frame]; }
            body.color = Person.Mood == Mood.Broken ? new Color(.72f, .72f, .75f) : Color.white;
            body.sortingOrder = 600 - Mathf.RoundToInt(transform.position.y * 25);
            marker.sortingOrder = body.sortingOrder - 1;
            marker.color = selected ? new Color(1, .82f, .22f, .95f)
                : Person.Role == Role.Chief ? new Color(.2f, .5f, .9f, .65f)
                : Person.Role == Role.Builder ? new Color(.88f, .3f, .3f, .65f) : new Color(.2f, .47f, .26f, .55f);
            label.text = Person.Title;
            cargo.text = carrying ? (Person.Job == Job.Harvest ? $"+{Person.Yield}" : "+basket") : "";
            label.gameObject.SetActive(selected);
        }
    }
}
