using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zeldexile.Combat;
using Zeldexile.Domain;
using Zeldexile.Player;
using Zeldexile.World;
using Zeldexile.World.Atmosphere;

namespace Zeldexile.UI
{
    /// <summary>
    /// Prototype HUD drawn with IMGUI, laid out like the reference: hearts top-left, the equipped rune,
    /// weapon, bow and shield under them, the stamina wheel beside the character, clock and temperature
    /// bottom-right, and button prompts only when there is something to do. The pouch, armor, materials,
    /// food, cooking and prayer screens live here too. Final UI will be its own layer.
    /// </summary>
    public sealed class GameHud : MonoBehaviour
    {
        public PlayerCharacter Character;
        public PlayerMotor Motor;
        public PlayerCaster Caster;
        public PlayerCombat Combat;
        public PlayerInteractor Interactor;
        public PlayerInputReader Input;
        public Camera ViewCamera;
        public PlayerClimate Climate;
        public PlayerTargeting Targeting;

        private enum Screen
        {
            None,
            Pouch,
            Cooking,
            Statue
        }

        private struct Popup
        {
            public Vector3 World;
            public string Text;
            public float Born;
        }

        private readonly List<Popup> _popups = new List<Popup>();
        private float _bannerUntil;
        private string _banner;

        private static readonly string[] Tabs = { "Armas", "Arcos", "Escudos", "Armaduras", "Materiais", "Comida", "Runas" };

        private Screen _screen;
        private int _tab;
        private Vector2 _scroll;
        private readonly List<MaterialDefinition> _pot = new List<MaterialDefinition>();
        private CookedDish _lastDish;
        private Texture2D _white;
        private GUIStyle _label;
        private GUIStyle _small;
        private GUIStyle _title;
        private GUIStyle _button;
        private bool _subscribed;

        private void Start()
        {
            _white = Texture2D.whiteTexture;
            Subscribe();
        }

        private void OnEnable()
        {
            ImpactResolver.Damaged += OnDamaged;
            Subscribe();
        }

        private void Subscribe()
        {
            if (_subscribed || Character == null) return;
            _subscribed = true;
            if (Motor != null) Motor.PerfectDodged += OnPerfectDodge;
            Character.Announced += Announce;
            CookingPot.Opened += OnPotOpened;
            GoddessStatue.Opened += OnStatueOpened;
        }

        private void OnDisable()
        {
            ImpactResolver.Damaged -= OnDamaged;
            if (!_subscribed) return;
            _subscribed = false;
            if (Motor != null) Motor.PerfectDodged -= OnPerfectDodge;
            if (Character != null) Character.Announced -= Announce;
            CookingPot.Opened -= OnPotOpened;
            GoddessStatue.Opened -= OnStatueOpened;
        }

        private void OnPotOpened(CookingPot _) => Open(Screen.Cooking);
        private void OnStatueOpened(GoddessStatue _) => Open(Screen.Statue);

        private void OnDamaged(Health target, float amount, ElementalImpact impact, bool special)
        {
            if (Character != null && target == Character.Health) return;
            var color = ColorUtility.ToHtmlStringRGB(World.Palette.ForElement(impact.Element));
            var text = special ? $"<color=#ffd84a><b>{amount:0}!</b></color>" : $"<color=#{color}><b>{amount:0}</b></color>";
            _popups.Add(new Popup { World = target.transform.position + Vector3.up * 2f, Text = text, Born = Time.unscaledTime });
        }

        private void OnPerfectDodge() => Announce("ESQUIVA PERFEITA — Rajada!");

        private void Announce(string message)
        {
            _banner = message;
            _bannerUntil = Time.unscaledTime + 1.8f;
        }

        /// <summary>The hero kneels at the pot and at the statue while their screens are open.</summary>
        public bool Kneeling => _screen == Screen.Cooking || _screen == Screen.Statue;

        private void Open(Screen screen)
        {
            _screen = screen;
            _pot.Clear();
            _lastDish = null;
            Input.GameplayBlocked = screen != Screen.None;
        }

        private void Update()
        {
            if (Input.MenuPressed) Open(_screen == Screen.None ? Screen.Pouch : Screen.None);
        }

        private void EnsureStyles()
        {
            if (_label != null) return;
            _label = new GUIStyle(GUI.skin.label) { fontSize = 15, richText = true };
            _small = new GUIStyle(GUI.skin.label) { fontSize = 13, richText = true, wordWrap = true };
            _title = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, richText = true };
            _button = new GUIStyle(GUI.skin.button) { richText = true, alignment = TextAnchor.MiddleLeft, fontSize = 13 };
        }

        private void OnGUI()
        {
            if (Character == null) return;
            EnsureStyles();
            DrawFocusOverlay();
            DrawEnemies();
            DrawPopups();
            DrawStaminaWheel();
            DrawHearts();
            DrawEquipment();
            DrawWorldPanel();
            DrawBanner();
            DrawLog();
            switch (_screen)
            {
                case Screen.Pouch: DrawPouch(); break;
                case Screen.Cooking: DrawCooking(); break;
                case Screen.Statue: DrawStatue(); break;
                default: DrawPromptsAndCrosshair(); break;
            }
        }

        // ---------- HUD ----------

        /// <summary>Hearts in quarters, ten per row; temporary hearts in yellow after the real ones.</summary>
        private void DrawHearts()
        {
            var h = Character.Health;
            var containers = Mathf.CeilToInt(h.Max / LifeModel.PointsPerHeart);
            var temporary = Mathf.CeilToInt(h.Temporary / LifeModel.PointsPerHeart);
            const float size = 20f, gap = 4f;
            var x0 = 20f;
            var y0 = 18f;
            var index = 0;
            for (var i = 0; i < containers + temporary; i++, index++)
            {
                var col = index % 10;
                var row = index / 10;
                var r = new Rect(x0 + col * (size + gap), y0 + row * (size + gap), size, size);
                var isTemporary = i >= containers;
                var points = isTemporary ? h.Temporary - (i - containers) * LifeModel.PointsPerHeart : h.Current - i * LifeModel.PointsPerHeart;
                var fill = Mathf.Clamp01(points / LifeModel.PointsPerHeart);
                Fill(r, new Color(0f, 0f, 0f, 0.5f));
                var inner = new Rect(r.x + 2, r.y + 2, r.width - 4, r.height - 4);
                Fill(new Rect(inner.x, inner.y + inner.height * (1f - fill), inner.width, inner.height * fill),
                    isTemporary ? new Color(1f, 0.85f, 0.2f) : new Color(0.9f, 0.18f, 0.2f));
            }
        }

        /// <summary>Rune, weapon, bow with arrows and shield, with durability bars, under the hearts.</summary>
        private void DrawEquipment()
        {
            var rows = 1 + (Mathf.CeilToInt(Character.Health.Max / LifeModel.PointsPerHeart) + Mathf.CeilToInt(Character.Health.Temporary / LifeModel.PointsPerHeart) - 1) / 10;
            var y = 18f + rows * 24f + 10f;
            const float w = 190f, hgt = 34f;

            var rune = Caster != null ? Caster.SelectedRune : null;
            if (rune != null)
            {
                var r = new Rect(20, y, w, hgt);
                Fill(r, new Color(0f, 0f, 0f, 0.55f));
                Outline(r, World.Palette.ForElement(rune.Base.Element));
                var cd = Caster.CooldownRemaining(Caster.Selected);
                var cdText = cd > 0f ? $" <size=11>({cd:0.0} s)</size>" : "";
                GUI.Label(new Rect(r.x + 8, r.y + 2, r.width - 12, r.height), $"<size=11>RUNA {Caster.Selected + 1}</size>\n<b>{rune.Name}</b>{cdText}", _small);
                y += hgt + 6f;
            }

            y = GearBox(y, "ARMA", Character.Weapon, w, hgt, Character.Weapon != null ? $"ataque {Character.Weapon.Attack}" : "mãos vazias");
            y = GearBox(y, "ARCO", Character.Bow, w, hgt, Character.Bow != null ? $"{Character.Arrows} flecha(s)" : "");
            GearBox(y, "ESCUDO", Character.Shield, w, hgt, Character.Shield != null ? $"guarda {Character.Shield.Guard}" : "");
        }

        private float GearBox(float y, string slot, WeaponInstance item, float w, float hgt, string detail)
        {
            var r = new Rect(20, y, w, hgt);
            Fill(r, new Color(0f, 0f, 0f, 0.55f));
            if (item == null)
            {
                GUI.Label(new Rect(r.x + 8, r.y + 2, r.width - 12, r.height), $"<size=11>{slot}</size>\n<color=#999>{(detail.Length > 0 ? detail : "—")}</color>", _small);
                return y + hgt + 6f;
            }

            var color = item.IsResting ? "#88c" : item.BadlyDamaged ? "#ff6a4a" : "#eee";
            var resting = item.IsResting ? $" <size=11>({item.RechargeRemaining / 60f:0}:{item.RechargeRemaining % 60f:00})</size>" : "";
            GUI.Label(new Rect(r.x + 8, r.y + 2, r.width - 12, r.height), $"<size=11>{slot}  <color=#bbb>{detail}</color></size>\n<color={color}><b>{item.Name}</b></color>{resting}", _small);
            var bar = new Rect(r.x + 2, r.yMax - 4, (r.width - 4) * Mathf.Clamp01(item.DurabilityNormalized), 3);
            Fill(bar, item.BadlyDamaged ? new Color(1f, 0.4f, 0.3f) : new Color(0.5f, 0.9f, 0.5f));
            return y + hgt + 6f;
        }

        /// <summary>Stamina as a ring beside the character, shown only while it is not full. Yellow = temporary.</summary>
        private void DrawStaminaWheel()
        {
            var s = Character.Stamina;
            if (s.IsFull && !s.Exhausted) return;
            var p = ViewCamera.WorldToScreenPoint(Character.transform.position + Vector3.up * 1.4f);
            if (p.z < 0f) return;
            var center = new Vector2(p.x + 70f, UnityEngine.Screen.height - p.y - 10f);
            const int pips = 36;
            var wheels = Mathf.CeilToInt(s.Wheels + s.Temporary / StaminaModel.WheelUnits - 0.001f);
            for (var wheel = 0; wheel < Mathf.Max(1, wheels); wheel++)
            {
                var radius = 22f + wheel * 9f;
                var start = wheel * StaminaModel.WheelUnits;
                for (var i = 0; i < pips; i++)
                {
                    var unitsAtPip = start + (i + 1f) / pips * StaminaModel.WheelUnits;
                    Color color;
                    if (unitsAtPip <= s.Max && unitsAtPip <= s.Current)
                        color = s.Exhausted ? new Color(0.95f, 0.45f, 0.2f) : Color.Lerp(new Color(0.95f, 0.85f, 0.25f), new Color(0.4f, 0.9f, 0.4f), s.Normalized);
                    else if (unitsAtPip > s.Max && unitsAtPip - s.Max <= s.Temporary)
                        color = new Color(1f, 0.85f, 0.2f);
                    else if (unitsAtPip <= s.Max)
                        color = new Color(0f, 0f, 0f, 0.45f);
                    else continue;
                    var a = Mathf.PI / 2f - i / (float)pips * Mathf.PI * 2f;
                    var pos = center + new Vector2(Mathf.Cos(a), -Mathf.Sin(a)) * radius;
                    Fill(new Rect(pos.x - 3f, pos.y - 3f, 6f, 6f), color);
                }
            }
        }

        /// <summary>Clock, weather with forecast and temperature, bottom right.</summary>
        private void DrawWorldPanel()
        {
            var atmosphere = AtmosphereController.Current;
            if (atmosphere == null) return;
            var forecast = $"{WeatherTable.DisplayName(atmosphere.Weather.Forecast(1))} → {WeatherTable.DisplayName(atmosphere.Weather.Forecast(2))}";
            var line = $"<b>{atmosphere.Clock.Display}</b>  Dia {atmosphere.Clock.Day + 1}   {WeatherTable.DisplayName(atmosphere.CurrentWeather)}  <size=11>(depois: {forecast})</size>";
            if (atmosphere.IsBloodMoonNight) line += "  <color=#ff6060><b>Lua de Sangue</b></color>";
            if (Climate != null)
            {
                var c = Climate.Celsius;
                var color = Climate.InDanger ? (Climate.ColdLevel > 0 ? "#8fd0ff" : "#ff9a60") : c > 30f ? "#ffb070" : "#e8e8e8";
                var warn = Climate.InDanger ? (Climate.ColdLevel > 0 ? " <b>congelando!</b>" : " <b>calor demais!</b>") : Climate.NearFire ? " (perto do fogo)" : "";
                line += $"\n<color={color}>{c:0} °C{warn}</color>";
                if (Character.ActiveEffect != FoodEffect.None)
                    line += $"   <color=#cfc>{Cooking.EffectName(Character.ActiveEffect)} {Character.ActiveEffectLevel} · {Character.EffectRemaining / 60f:0}:{Character.EffectRemaining % 60f:00}</color>";
            }

            var rect = new Rect(UnityEngine.Screen.width - 480, UnityEngine.Screen.height - 64, 464, 50);
            Fill(rect, new Color(0f, 0f, 0f, 0.35f));
            GUI.Label(new Rect(rect.x + 8, rect.y + 2, rect.width - 8, rect.height - 2), $"<color=#eee>{line}</color>", _small);
        }

        private void DrawPopups()
        {
            const float life = 0.9f;
            for (var i = _popups.Count - 1; i >= 0; i--)
            {
                var age = Time.unscaledTime - _popups[i].Born;
                if (age > life)
                {
                    _popups.RemoveAt(i);
                    continue;
                }

                var p = ViewCamera.WorldToScreenPoint(_popups[i].World + Vector3.up * age * 1.2f);
                if (p.z < 0f) continue;
                GUI.Label(new Rect(p.x - 40, UnityEngine.Screen.height - p.y, 160, 24), _popups[i].Text, _label);
            }
        }

        private void DrawBanner()
        {
            if (Time.unscaledTime > _bannerUntil || _banner == null) return;
            GUI.Label(new Rect(UnityEngine.Screen.width / 2f - 240, UnityEngine.Screen.height * 0.25f, 480, 30), $"<color=#ffe27a><size=20><b>{_banner}</b></size></color>",
                new GUIStyle(_label) { alignment = TextAnchor.MiddleCenter });
        }

        private void DrawFocusOverlay()
        {
            if (Motor == null || (!Motor.Focusing && !Character.CounterActive)) return;
            var tint = Motor.Focusing ? new Color(0.35f, 0.6f, 1f, 0.12f) : new Color(1f, 0.85f, 0.3f, 0.1f);
            const float edge = 60f;
            Fill(new Rect(0, 0, UnityEngine.Screen.width, edge), tint);
            Fill(new Rect(0, UnityEngine.Screen.height - edge, UnityEngine.Screen.width, edge), tint);
            Fill(new Rect(0, 0, edge, UnityEngine.Screen.height), tint);
            Fill(new Rect(UnityEngine.Screen.width - edge, 0, edge, UnityEngine.Screen.height), tint);
        }

        private void DrawLog()
        {
            var y = UnityEngine.Screen.height - 110f - Character.Log.Count * 20f;
            foreach (var line in Character.Log)
            {
                GUI.Label(new Rect(20, y, 420, 20), $"<color=#f0e0a0>{line}</color>", _small);
                y += 20f;
            }
        }

        /// <summary>Button prompts appear only with something to do: an object nearby, a wall, the water.</summary>
        private void DrawPromptsAndCrosshair()
        {
            var c = new Vector2(UnityEngine.Screen.width / 2f, UnityEngine.Screen.height / 2f);
            if (Combat != null && Combat.Aiming)
            {
                Fill(new Rect(c.x - 1, c.y - 8, 2, 16), new Color(1, 1, 1, 0.9f));
                Fill(new Rect(c.x - 8, c.y - 1, 16, 2), new Color(1, 1, 1, 0.9f));
            }
            else
            {
                Fill(new Rect(c.x - 1, c.y - 1, 3, 3), new Color(1, 1, 1, 0.6f));
            }

            var prompts = new List<string>();
            if (Interactor != null && Interactor.Current != null && Interactor.Current.Prompt.Length > 0)
                prompts.Add($"<b>E</b> {Interactor.Current.Prompt}");
            switch (Motor.Current)
            {
                case PlayerMotor.State.Climbing: prompts.Add("<b>Espaço</b> Pular   <b>S</b> Soltar"); break;
                case PlayerMotor.State.Airborne: prompts.Add("<b>Espaço</b> Planador"); break;
                case PlayerMotor.State.Gliding: prompts.Add("<b>Espaço</b> Fechar planador"); break;
                case PlayerMotor.State.Swimming: prompts.Add("<b>Shift</b> Dash"); break;
                default:
                    if (Combat != null && Combat.Guarding) prompts.Add("<b>Espaço</b> Defesa perfeita");
                    else if (Targeting != null && Targeting.Target != null) prompts.Add("<b>Espaço</b> Esquivar   <b>Q (segurar)</b> Escudo");
                    break;
            }

            if (prompts.Count > 0)
            {
                var text = string.Join("     ", prompts);
                var style = new GUIStyle(_label) { alignment = TextAnchor.MiddleCenter };
                var rect = new Rect(UnityEngine.Screen.width / 2f - 300, UnityEngine.Screen.height - 44, 600, 28);
                Fill(rect, new Color(0f, 0f, 0f, 0.4f));
                GUI.Label(rect, $"<color=#eee>{text}</color>", style);
            }

            const string help = "<b>Clique</b> atacar · <b>F</b> arremessar · <b>Botão direito</b> arco · <b>R</b> runa (<b>1-4</b> escolhe) · <b>Q</b> travar / segurar = escudo · <b>E</b> usar · <b>Scroll</b> zoom · <b>Tab</b> bolsa";
            GUI.Label(new Rect(UnityEngine.Screen.width / 2f - 420, 14, 840, 24), $"<color=#bbb><size=12>{help}</size></color>", new GUIStyle(_small) { alignment = TextAnchor.MiddleCenter });
        }

        /// <summary>Lock-on arrow and name; a health bar only while the creature is awake or targeted.</summary>
        private void DrawEnemies()
        {
            if (ViewCamera == null) return;
            foreach (var e in EnemyBrain.Active)
            {
                if (e == null || e.Health.IsDead) continue;
                var p = ViewCamera.WorldToScreenPoint(e.transform.position + Vector3.up * 2.3f);
                if (p.z < 0f || p.z > 40f) continue;
                var locked = Targeting != null && Targeting.Target == e;
                var r = new Rect(p.x - 30, UnityEngine.Screen.height - p.y, 60, 6);
                if (locked)
                {
                    GUI.Label(new Rect(p.x - 10, r.y - 46, 30, 30), "<color=#ffb03c><size=24><b>▼</b></size></color>", _label);
                    GUI.Label(new Rect(p.x - 70, r.y + 8, 140, 18), $"<size=11><color=#eee>{e.DisplayName}</color></size>", new GUIStyle(_small) { alignment = TextAnchor.MiddleCenter });
                }

                if (e.IsAware || locked) Bar(r, e.Health.Current / e.Health.Max, World.Palette.ForTier(e.Tier), null);
                if (e.IsWindingUp) GUI.Label(new Rect(p.x - 8, r.y - 40, 30, 30), "<color=#ff5a3c><size=26><b>!</b></size></color>", _label);
                else if (e.IsSleeping) GUI.Label(new Rect(p.x - 8, r.y - 34, 30, 24), "<color=#9aa><size=14>zz</size></color>", _label);
                var status = e.Health.IsFrozen ? "congelado" : e.Health.IsBurning ? "queimando" : e.IsStunned ? "atordoado" : "";
                if (status.Length > 0) GUI.Label(new Rect(r.x - 10, r.y - 18, 90, 18), $"<size=11>{status}</size>", _small);
            }
        }

        // ---------- Pouch ----------

        private void DrawPouch()
        {
            var rect = new Rect(UnityEngine.Screen.width / 2f - 430, 60, 860, UnityEngine.Screen.height - 160);
            Fill(rect, new Color(0.06f, 0.07f, 0.09f, 0.95f));
            GUILayout.BeginArea(new Rect(rect.x + 16, rect.y + 12, rect.width - 32, rect.height - 24));
            GUILayout.BeginHorizontal();
            _tab = GUILayout.Toolbar(_tab, Tabs, GUILayout.Height(30));
            if (GUILayout.Button("Fechar (Tab)", GUILayout.Width(110), GUILayout.Height(30))) Open(Screen.None);
            if (GUILayout.Button("Sair do jogo", GUILayout.Width(110), GUILayout.Height(30))) Application.Quit();
            GUILayout.EndHorizontal();
            GUILayout.Label($"<size=12><color=#ccc>Rupees {Character.Rupees} · Sementes Korok {Character.KorokSeeds} · Orbes {Character.SpiritOrbs} · Flechas {Character.Arrows} · Defesa {Character.Defense}</color></size>", _small);
            GUILayout.Space(6);
            _scroll = GUILayout.BeginScrollView(_scroll);
            switch (_tab)
            {
                case 0: DrawGearTab(GearKind.Weapon); break;
                case 1: DrawGearTab(GearKind.Bow); break;
                case 2: DrawGearTab(GearKind.Shield); break;
                case 3: DrawArmorTab(); break;
                case 4: DrawMaterialsTab(); break;
                case 5: DrawFoodTab(); break;
                default: DrawRunesTab(); break;
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawGearTab(GearKind kind)
        {
            var items = Character.Gear.Items(kind);
            var equipped = Character.SlotFor(kind);
            GUILayout.BeginHorizontal();
            GUILayout.Label($"{GearInventory.KindName(kind)}: {items.Count}/{Character.Gear.Slots(kind)} espaços", _title);
            var cost = Character.Gear.NextUpgradeCost(kind);
            if (cost > 0)
            {
                GUI.enabled = Character.KorokSeeds >= cost;
                if (GUILayout.Button($"+1 espaço por {cost} semente(s)", GUILayout.Width(220), GUILayout.Height(26))) Character.UpgradePouch(kind);
                GUI.enabled = true;
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(4);

            foreach (var w in items.ToList())
            {
                GUILayout.BeginHorizontal();
                var mark = w == equipped ? "<color=#8f8>▶</color> " : "   ";
                var state = w.IsResting ? " <color=#88c>(recarregando)</color>" : w.BadlyDamaged ? " <color=#f86>(muito gasta)</color>" : "";
                var mod = w.Modifier != WeaponModifier.None ? $" <color={(w.YellowModifier ? "#fd5" : "#9cf")}>{w.ModifierText}</color>" : "";
                var stat = kind == GearKind.Shield ? $"guarda {w.Guard}" : $"ataque {w.Attack}";
                GUILayout.Label($"{mark}<b>{w.Name}</b>  <size=12>{WeaponCatalog.ClassName(w.Class)} · {stat} · durabilidade {w.Durability:0}/{w.MaxDurability}{mod}{state}</size>", _small, GUILayout.Width(560));
                GUI.enabled = w != equipped && w.IsUsable;
                if (GUILayout.Button("Equipar", GUILayout.Width(80))) Character.Equip(w);
                GUI.enabled = !w.Definition.Anchor;
                if (GUILayout.Button("Largar", GUILayout.Width(80)))
                {
                    Character.RemoveWeapon(w, $"Largou {w.Name}.");
                    WeaponPickup.Spawn(w, Character.transform.position + Character.transform.forward * 1.2f);
                }
                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }

            if (items.Count == 0) GUILayout.Label("Nada aqui. Armas vêm do mundo: inimigos, baús e o chão.", _small);
        }

        private void DrawArmorTab()
        {
            GUILayout.Label("Vestindo", _title);
            foreach (var slot in new[] { ArmorSlot.Head, ArmorSlot.Body, ArmorSlot.Legs })
            {
                var piece = Character.Worn(slot);
                GUILayout.BeginHorizontal();
                GUILayout.Label($"<b>{Armor.SlotName(slot)}:</b> " + (piece == null ? "<color=#999>nada</color>" : $"{piece.Name} {piece.Stars}  <size=12>defesa {piece.Defense} · {Describe(piece.Set)}</size>"), _small, GUILayout.Width(640));
                if (piece != null && GUILayout.Button("Tirar", GUILayout.Width(80))) Character.TakeOff(slot);
                GUILayout.EndHorizontal();
            }

            if (Armor.SetBonusActive(Character.Head, Character.Body, Character.Legs))
                GUILayout.Label($"<color=#8f8>Bônus do conjunto ativo: {StatBlock.Describe(Character.Head.Set.SetBonus.Stat, Character.Head.Set.SetBonus.Value)}</color>", _small);

            GUILayout.Space(10);
            GUILayout.Label($"Guarda-roupa ({Character.Wardrobe.Count})", _title);
            foreach (var piece in Character.Wardrobe.ToList())
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"<b>{piece.Name}</b> {piece.Stars}  <size=12>{Armor.SlotName(piece.Slot)} · defesa {piece.Defense} · {Describe(piece.Set)}</size>", _small, GUILayout.Width(640));
                if (GUILayout.Button("Vestir", GUILayout.Width(80))) Character.Wear(piece);
                GUILayout.EndHorizontal();
            }

            if (Character.Wardrobe.Count == 0) GUILayout.Label("Armaduras vêm de vilas, provas e missões, nunca de drop aleatório.", _small);
            GUILayout.Space(8);
            GUILayout.Label($"<size=12><color=#aaa>Efeitos ativos: {ActiveStats()}</color></size>", _small);
        }

        private static string Describe(ArmorSetDefinition set)
        {
            var effect = set.Effect.Value == 0f ? "sem efeito" : StatBlock.Describe(set.Effect.Stat, set.Effect.Value);
            var bonus = set.HasSetBonus ? $"; conjunto: {StatBlock.Describe(set.SetBonus.Stat, set.SetBonus.Value)}" : "";
            return effect + bonus;
        }

        private string ActiveStats()
        {
            var parts = new List<string>();
            foreach (StatId id in System.Enum.GetValues(typeof(StatId)))
            {
                var v = Character.Stats.Get(id);
                if (v != 0f) parts.Add(StatBlock.Describe(id, v));
            }

            return parts.Count == 0 ? "nenhum" : string.Join(", ", parts);
        }

        private void DrawMaterialsTab()
        {
            foreach (MaterialCategory category in System.Enum.GetValues(typeof(MaterialCategory)))
            {
                var inCategory = Character.Materials.Where(p => p.Key.Category == category && p.Value > 0).ToList();
                if (inCategory.Count == 0) continue;
                GUILayout.Label(MaterialCatalog.CategoryName(category), _title);
                foreach (var pair in inCategory)
                {
                    var m = pair.Key;
                    var effect = m.Effect != FoodEffect.None ? $" · {Cooking.EffectName(m.Effect)} {m.Potency}" : "";
                    var heal = m.HealPoints > 0 ? $" · cura {m.HealPoints / 4f:0.##} cru" : "";
                    GUILayout.Label($"<b>{m.Name}</b> ×{pair.Value}  <size=12>venda {m.SellPrice}{heal}{effect}</size>", _small);
                }
            }

            if (Character.Materials.Count == 0) GUILayout.Label("Colha frutas, cogumelos e partes de monstro pelo mundo.", _small);
        }

        private void DrawFoodTab()
        {
            GUILayout.Label($"Comida ({Character.Food.Count})", _title);
            foreach (var dish in Character.Food.ToList())
            {
                GUILayout.BeginHorizontal();
                var crit = dish.Critical ? " <color=#fd5>★</color>" : "";
                GUILayout.Label($"<b>{dish.Name}</b>{crit}  <size=12>{dish.Describe()}</size>", _small, GUILayout.Width(640));
                if (GUILayout.Button("Comer", GUILayout.Width(80))) Character.Eat(dish);
                GUILayout.EndHorizontal();
            }

            if (Character.Food.Count == 0) GUILayout.Label("Cozinhe numa panela sobre o fogo. Não existe cura comprável.", _small);
        }

        private void DrawRunesTab()
        {
            GUILayout.Label("As quatro runas estão com você desde o início. Escolha com 1-4 e lance com R.", _small);
            GUILayout.Space(6);
            for (var i = 0; i < Character.Runes.Count; i++)
            {
                var rune = Character.Runes[i];
                var st = Caster != null ? Caster.StatsFor(i) : rune.Base;
                GUILayout.Label($"<b>{i + 1}. {rune.Name}</b> ({ElementalReactions.DisplayName(st.Element)}) — {rune.Description}", _label);
                GUILayout.Label($"   {st.Damage:0} de dano, área {st.Radius:0.#} m, recarga {st.Cooldown:0.#} s" + (st.Chains > 0 ? $", salta {st.Chains}×" : ""), _small);
            }
        }

        // ---------- Cooking ----------

        private void DrawCooking()
        {
            var rect = new Rect(UnityEngine.Screen.width / 2f - 430, 60, 860, UnityEngine.Screen.height - 160);
            Fill(rect, new Color(0.08f, 0.06f, 0.05f, 0.95f));
            GUILayout.BeginArea(new Rect(rect.x + 16, rect.y + 12, rect.width - 32, rect.height - 24));
            GUILayout.BeginHorizontal();
            GUILayout.Label("Panela", _title);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Fechar (Tab)", GUILayout.Width(110), GUILayout.Height(30))) Open(Screen.None);
            GUILayout.EndHorizontal();

            var atmosphere = AtmosphereController.Current;
            var bloodWindow = atmosphere != null && Cooking.InBloodMoonWindow(atmosphere.Clock.Hour, atmosphere.IsBloodMoonNight);
            GUILayout.Label($"Até {Cooking.MaxIngredients} ingredientes. Um efeito por prato; dois efeitos diferentes se anulam. Criaturinha + parte de monstro = elixir." +
                            (bloodWindow ? " <color=#f66><b>Lua de Sangue: sucesso crítico garantido.</b></color>" : ""), _small);
            GUILayout.Space(6);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Na panela: " + (_pot.Count == 0 ? "<color=#999>vazia</color>" : string.Join(", ", _pot.Select(m => m.Name))), _label, GUILayout.Width(560));
            GUI.enabled = _pot.Count > 0;
            if (GUILayout.Button("Cozinhar", GUILayout.Width(100), GUILayout.Height(28)))
            {
                var critical = bloodWindow || Random.value < Cooking.CriticalChance;
                _lastDish = Character.Cook(_pot.ToList(), critical);
                _pot.Clear();
            }
            if (GUILayout.Button("Esvaziar", GUILayout.Width(90), GUILayout.Height(28))) _pot.Clear();
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            if (_lastDish != null) GUILayout.Label($"<color=#fd9>Saiu: <b>{_lastDish.Name}</b> — {_lastDish.Describe()}{(_lastDish.Critical ? " (crítico!)" : "")}</color>", _small);
            GUILayout.Space(8);

            _scroll = GUILayout.BeginScrollView(_scroll);
            foreach (var pair in Character.Materials.ToList())
            {
                var m = pair.Key;
                var inPot = _pot.Count(x => x == m);
                var left = pair.Value - inPot;
                if (left <= 0 && inPot == 0) continue;
                GUILayout.BeginHorizontal();
                var effect = m.Effect != FoodEffect.None ? $" · {Cooking.EffectName(m.Effect)} {m.Potency}" : "";
                var kind = m.Inedible ? " · <color=#f86>não é comida</color>" : m.IsCritter ? " · criaturinha" : m.IsMonsterPart ? " · parte de monstro" : "";
                GUILayout.Label($"<b>{m.Name}</b> ×{left}  <size=12>cura {m.HealPoints / 4f:0.##}{effect}{kind}</size>", _small, GUILayout.Width(560));
                GUI.enabled = left > 0 && _pot.Count < Cooking.MaxIngredients;
                if (GUILayout.Button("+", GUILayout.Width(36))) _pot.Add(m);
                GUI.enabled = inPot > 0;
                if (GUILayout.Button("−", GUILayout.Width(36))) _pot.Remove(m);
                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        // ---------- Statue ----------

        private void DrawStatue()
        {
            var rect = new Rect(UnityEngine.Screen.width / 2f - 300, 120, 600, 220);
            Fill(rect, new Color(0.05f, 0.06f, 0.10f, 0.95f));
            GUILayout.BeginArea(new Rect(rect.x + 16, rect.y + 12, rect.width - 32, rect.height - 24));
            GUILayout.Label("Estátua da Deusa", _title);
            GUILayout.Label($"Orbes Espirituais: <b>{Character.SpiritOrbs}</b>. Quatro orbes valem um coração ou um quinto de roda de stamina.", _small);
            GUILayout.Label($"Corações: {Character.Life.Containers}/{LifeModel.MaxHearts} · Rodas: {Character.Stamina.Wheels:0.##}/{StaminaModel.MaxWheels}", _small);
            GUILayout.Space(10);
            GUILayout.BeginHorizontal();
            GUI.enabled = Character.CanPray && Character.Life.Containers < LifeModel.MaxHearts;
            if (GUILayout.Button("Coração (4 orbes)", GUILayout.Height(30))) Character.Pray(true);
            GUI.enabled = Character.CanPray && Character.Stamina.Vessels < StaminaModel.MaxVessels;
            if (GUILayout.Button("Stamina (4 orbes)", GUILayout.Height(30))) Character.Pray(false);
            GUI.enabled = true;
            if (GUILayout.Button("Fechar", GUILayout.Width(100), GUILayout.Height(30))) Open(Screen.None);
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        // ---------- helpers ----------

        private void Bar(Rect r, float t, Color color, string text)
        {
            Fill(r, new Color(0, 0, 0, 0.6f));
            Fill(new Rect(r.x + 1, r.y + 1, (r.width - 2) * Mathf.Clamp01(t), r.height - 2), color);
            if (!string.IsNullOrEmpty(text)) GUI.Label(new Rect(r.x + 6, r.y - 3, r.width, r.height + 6), $"<size=12>{text}</size>", _small);
        }

        private void Fill(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, _white);
            GUI.color = old;
        }

        private void Outline(Rect r, Color c)
        {
            Fill(new Rect(r.x, r.y, r.width, 2), c);
            Fill(new Rect(r.x, r.yMax - 2, r.width, 2), c);
            Fill(new Rect(r.x, r.y, 2, r.height), c);
            Fill(new Rect(r.xMax - 2, r.y, 2, r.height), c);
        }
    }
}
