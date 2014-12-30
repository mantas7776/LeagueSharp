using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using LeagueSharp;
using LeagueSharp.Common;
using SharpDX;


namespace Kata
{
    class Program
    {

        //shortcuts
        private static Obj_AI_Hero Player = ObjectManager.Player;
        // spells
        private static Spell Q;
        private static Spell W;
        private static Spell E;
        private static Spell R;
        private static bool InUlt = false;
        private static SpellSlot IgniteSlot;
        private static List<Spell> ComboList = new List<Spell>();

        //menu & orbwalking
        private static Menu Men;
        private static Orbwalking.Orbwalker Orbwalker;
        private static Menu OrbwalkingMenu;
        private static Menu TSMenu;
        private static TargetSelector TS;

        static void Main(string[] args)
        {
            CustomEvents.Game.OnGameLoad += Game_OnGameLoad;
        }

        private static void Game_OnGameLoad(EventArgs args)
        {
            if (ObjectManager.Player.BaseSkinName != "Katarina") return;
            Game.PrintChat("Simple katarina is loading...");
            Q = new Spell(SpellSlot.Q, 675f);
            W = new Spell(SpellSlot.W, 375f);
            E = new Spell(SpellSlot.E, 700f);
            R = new Spell(SpellSlot.R, 550f);
            IgniteSlot = ObjectManager.Player.GetSpellSlot("SummonerDot");

            ComboList.Add(Q);
            ComboList.Add(E);
            ComboList.Add(W);
            ComboList.Add(R);
            //init menu
            Men = new Menu("Katarina","Katarina", true);
            Men.AddToMainMenu();
            OrbwalkingMenu = new Menu("Orb Walker", "Orb Walker");
            Orbwalker = new Orbwalking.Orbwalker(OrbwalkingMenu);
            Men.AddSubMenu(OrbwalkingMenu);
            // ts menu
            TSMenu = new Menu("Target Selector", "Target Selector");
            TargetSelector.AddToMenu(TSMenu);
            Men.AddSubMenu(TSMenu);
            //packet cast item
            Men.AddItem(new MenuItem("Packet cast", "Packet cast")).SetValue<bool>(false);
            //drawing menu
            Men.AddSubMenu(new Menu("Drawing", "Drawing"));
            Men.SubMenu("Drawing").AddItem(new MenuItem("Range circle", "Range circle").SetValue(false));
            Men.SubMenu("Drawing").AddItem(new MenuItem("HP indicator", "HP indicator (broken)").SetValue(false));
            //farming settings
            Men.AddSubMenu(new Menu("Harass", "Harass"));
            Men.SubMenu("Harass").AddItem(new MenuItem("Mode", "Mode").SetValue(new StringList(new[] { "QEW", "QW" }, 0 )));

            LeagueSharp.Drawing.OnDraw += Drawing;
            LeagueSharp.Obj_AI_Base.OnPlayAnimation += PlayAnimation;
            Game.OnGameUpdate += Tick;
            
        }
        private static void Tick(EventArgs args)
        {
            if (ObjectManager.Player.IsDead) return;
            if (InUlt)
            {
                Orbwalker.SetAttack(false);
                Orbwalker.SetMovement(false);
                return;
            }
            else
            {
                Combo();
                KillSteal();
                Farm();
                Orbwalker.SetAttack(true);
                Orbwalker.SetMovement(true);
            }
        }

        private static void PlayAnimation(GameObject sender, GameObjectPlayAnimationEventArgs args)
        {
            if (sender.IsMe)
            {
                //System.IO.File.AppendAllText(@"c:\kata2.txt", args.Animation.ToString() + Environment.NewLine);
                if (args.Animation == "Spell4")
                {
                    InUlt = true;
                }
                else if (args.Animation == "Run" || args.Animation == "Idle1" || args.Animation == "Attack2" || args.Animation == "Attack1")
                { 
                    InUlt = false; 
                }
            }
        }

        private static void Drawing(EventArgs args)
        {
            if (Men.SubMenu("Drawing").Item("Range circle").GetValue<bool>() == true)
            {
                Render.Circle.DrawCircle(ObjectManager.Player.Position, E.Range, System.Drawing.Color.LimeGreen);
            }

            if (Men.SubMenu("Drawing").Item("HP indicator").GetValue<bool>() == true)
            {
                foreach (var hero in ObjectManager.Get<Obj_AI_Hero>().Where(hero => hero.IsValidTarget()))
                {
                    var health = hero.Health ;
                    var maxhealth = hero.MaxHealth;
                    Vector2 pos = hero.HPBarPosition;

                    // 34 x 9 y hpbar start
                    // 138 x end
                    var percent = health / maxhealth * 104f;
                    Vector2 start = new Vector2(34f, 8f);
                    Vector2 end = new Vector2(34f + percent, 8f);
                    LeagueSharp.Drawing.DrawLine(hero.HPBarPosition + start, hero.HPBarPosition + end, 1f , System.Drawing.Color.Red);
                }
            }
        }
        private static void KillSteal()
        {
            foreach (Obj_AI_Hero hero in ObjectManager.Get<Obj_AI_Hero>().Where(
                    hero => 
                    ObjectManager.Player.Distance(hero.ServerPosition) <= E.Range 
                    && hero.Name != ObjectManager.Player.Name 
                    && hero.IsValidTarget() 
                    && hero.IsEnemy 
                    && !hero.IsInvulnerable
                ))
            {
                var Qdmg = Q.GetDamage(hero);
                var Wdmg = W.GetDamage(hero);
                var Edmg = E.GetDamage(hero);
                var MarkDmg = Damage.CalcDamage(Player, hero, Damage.DamageType.Magical , Player.FlatMagicDamageMod * 0.15 + Player.Level * 15);
                if (hero.Health - Edmg < 0 && E.IsReady())
                {
                    E.Cast(hero, Men.Item("Packet cast").GetValue<bool>());
                    return;
                }
                if (hero.Health - Qdmg < 0 && Q.IsReady())
                {
                    Q.Cast(hero, Men.Item("Packet cast").GetValue<bool>());
                    return;
                }
                if (hero.Health - Edmg - Wdmg < 0 && E.IsReady() && W.IsReady())
                {
                    E.Cast(hero, Men.Item("Packet cast").GetValue<bool>());
                    W.Cast(Men.Item("Packet cast").GetValue<bool>());
                    return;
                }
                if (hero.Health - Edmg - Qdmg < 0 && E.IsReady() && Q.IsReady())
                {
                    E.Cast(hero, Men.Item("Packet cast").GetValue<bool>());
                    Q.Cast(hero, Men.Item("Packet cast").GetValue<bool>());
                    return;
                }
                if (hero.Health - Edmg - Wdmg - Qdmg - MarkDmg < 0 && E.IsReady() && Q.IsReady() && W.IsReady())
                {
                    E.Cast(hero, Men.Item("Packet cast").GetValue<bool>());
                    Q.Cast(hero, Men.Item("Packet cast").GetValue<bool>());
                    W.Cast(Men.Item("Packet cast").GetValue<bool>());
                    return;
                }
            }

            foreach (Obj_AI_Base target in ObjectManager.Get<Obj_AI_Base>().Where(
                    target => 
                    ObjectManager.Player.Distance(target.ServerPosition) <= E.Range 
                    && !target.IsMe
                    && target.IsTargetable
                    && !target.IsInvulnerable
                ))
            {
                foreach (Obj_AI_Hero focus in ObjectManager.Get<Obj_AI_Hero>().Where(
                    focus => 
                    focus.Distance(target.ServerPosition) <= Q.Range
                    && focus.IsEnemy
                    && !focus.IsInvulnerable
                    && focus.IsValidTarget()
                ))
                {
                    var Qdmg = Q.GetDamage(focus);
                    var Wdmg = W.GetDamage(focus);
                    //var PassiveDmg = Q.Level * 15 + ObjectManager.Player.
                    if (focus.Health - Qdmg < 0 && E.IsReady() && Q.IsReady())
                    {
                        E.Cast(target, Men.Item("Packet cast").GetValue<bool>());
                        Q.Cast(target, Men.Item("Packet cast").GetValue<bool>());
                        return;
                    }
                    if (focus.Distance(target.ServerPosition) <= W.Range && focus.Health - Qdmg - Wdmg < 0 && E.IsReady() && Q.IsReady()) 
                    {
                        E.Cast(target, Men.Item("Packet cast").GetValue<bool>());
                        Q.Cast(target, Men.Item("Packet cast").GetValue<bool>());
                        W.Cast(Men.Item("Packet cast").GetValue<bool>());
                    }

                }
            }

        }

        private static void Combo()
        {
            Obj_AI_Hero Target = TargetSelector.GetTarget(E.Range, TargetSelector.DamageType.Magical);
            //KeyBind combokey = Men.SubMenu("Orb Walker").Item("Orbwalk").GetValue<KeyBind>();
            //KeyBind mixedkey = Men.SubMenu("Orb Walker").Item("Mixed").GetValue<KeyBind>();

            if (Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.Combo && Target.IsValidTarget() && !InUlt)
            {
                if (Q.InRange(Target))
                {
                    if (Q.IsReady())
                    {
                        Q.Cast(Target, Men.Item("Packet cast").GetValue<bool>());
                    }
                    if (E.IsReady())
                    {
                        E.Cast(Target, Men.Item("Packet cast").GetValue<bool>());
                    }
                }
                else {
                    if (E.IsReady())
                    {
                        E.Cast(Target, Men.Item("Packet cast").GetValue<bool>());
                    }
                    if (Q.IsReady())
                    {
                        Q.Cast(Target, Men.Item("Packet cast").GetValue<bool>());
                    }
                }
                if (W.IsReady() && W.InRange(Target))
                {
                    Orbwalker.SetAttack(false);
                    Orbwalker.SetMovement(false);
                    W.Cast(Men.Item("Packet cast").GetValue<bool>());
                    return;
                }
                if (R.IsReady() && !InUlt && !E.IsReady())
                {
                    Orbwalker.SetAttack(false);
                    Orbwalker.SetMovement(false);
                    InUlt = true;
                    R.Cast(Men.Item("Packet cast").GetValue<bool>());
                    return;
                }
            }
            if (Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.Mixed && Target.IsValidTarget() && !InUlt)
            {
                if (Q.InRange(Target))
                {
                    if (Q.IsReady() && !InUlt)
                    {
                        Q.Cast(Target, Men.Item("Packet cast").GetValue<bool>());
                    }
                    if (E.IsReady() && !InUlt && Men.SubMenu("Harass").Item("Mode").GetValue<StringList>().SelectedIndex == 0)
                    {
                        E.Cast(Target, Men.Item("Packet cast").GetValue<bool>());
                    }
                }
                else
                {
                    if (E.IsReady() && !InUlt && Men.SubMenu("Harass").Item("Mode").GetValue<StringList>().SelectedIndex == 0)
                    {
                        E.Cast(Target, Men.Item("Packet cast").GetValue<bool>());
                    }
                    if (Q.IsReady() && !InUlt)
                    {
                        Q.Cast(Target, Men.Item("Packet cast").GetValue<bool>());
                    }
                }
                if (W.IsReady() && W.InRange(Target) && !InUlt)
                {
                    W.Cast(Men.Item("Packet cast").GetValue<bool>());
                }
            }

        }

        private static void Farm()
        {
            //KeyBind pushlanekey = Men.SubMenu("Orb Walker").Item("LaneClear").GetValue<KeyBind>();
            //KeyBind farmkey = Men.SubMenu("Orb Walker").Item("LastHit").GetValue<KeyBind>();

            if (Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.LastHit || Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.LaneClear)
            {
                foreach (var minion in ObjectManager.Get<Obj_AI_Minion>().Where(minion => minion.IsValidTarget() && minion.IsEnemy && minion.Distance(Player.ServerPosition) < E.Range))
                {
                    var Qdmg = Q.GetDamage(minion);
                    var Wdmg = W.GetDamage(minion);
                    var MarkDmg = Damage.CalcDamage(Player, minion, Damage.DamageType.Magical, Player.FlatMagicDamageMod * 0.15 + Player.Level * 15);

                    if (minion.Health - Qdmg <= 0 && minion.Distance(Player.ServerPosition) <= Q.Range && Q.IsReady()) { Q.Cast(minion, Men.Item("Packet cast").GetValue<bool>()); }
                    if (minion.Health - Wdmg <= 0 && minion.Distance(Player.ServerPosition) <= W.Range && W.IsReady()) { W.Cast(Men.Item("Packet cast").GetValue<bool>()); }
                    if (minion.Health - Wdmg - Qdmg <= 0 && minion.Distance(Player.ServerPosition) <= W.Range && Q.IsReady() && W.IsReady()) { Q.Cast(minion, Men.Item("Packet cast").GetValue<bool>()); W.Cast(Men.Item("Packet cast").GetValue<bool>()); }
                    if (minion.HasBuff("katarinaqmark") && minion.Health - Wdmg - MarkDmg <= 0 && minion.Distance(Player.ServerPosition) <= W.Range && W.IsReady())
                    {
                        W.Cast(Men.Item("Packet cast").GetValue<bool>());
                    }
                    if (minion.HasBuff("katarinaqmark") && minion.Health - Wdmg - Qdmg - MarkDmg <= 0 && minion.Distance(Player.ServerPosition) <= W.Range && Q.IsReady() && W.IsReady())
                    {
                        Q.Cast(minion, Men.Item("Packet cast").GetValue<bool>());
                    }

                }
            }
            if (Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.LaneClear)
            {
                foreach (var minion in ObjectManager.Get<Obj_AI_Minion>().Where(minion => minion.IsValidTarget() && minion.IsEnemy && minion.Distance(Player.ServerPosition) < E.Range))
                {
                    var Qdmg = Q.GetDamage(minion);
                    var Wdmg = W.GetDamage(minion);
                    var Edmg = E.GetDamage(minion);
                    var MarkDmg = Damage.CalcDamage(Player, minion, Damage.DamageType.Magical, Player.FlatMagicDamageMod * 0.15 + Player.Level * 15);

                    if (minion.Health - Edmg <= 0 && minion.Distance(Player.ServerPosition) <= E.Range && E.IsReady()) { W.Cast(minion, Men.Item("Packet cast").GetValue<bool>()); }

                    if (minion.Health - Wdmg - Qdmg - MarkDmg - Edmg <= 0 && minion.Distance(Player.ServerPosition) <= W.Range && E.IsReady() && Q.IsReady() && W.IsReady())
                    {
                        E.Cast(minion, Men.Item("Packet cast").GetValue<bool>());
                        Q.Cast(minion, Men.Item("Packet cast").GetValue<bool>());
                    }

                }
            }
        }

    }
}
