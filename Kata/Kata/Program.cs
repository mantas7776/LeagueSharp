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
        // spells && items
        private static Spell Q;
        private static Spell W;
        private static Spell E;
        private static Spell R;
        private static bool InUlt = false;
        private static SpellSlot IgniteSlot;
        private const int Meleerng = 125;
        private const float overkill = 0.9f;

        //menu & orbwalking
        private static Menu Men;
        private static Orbwalking.Orbwalker Orbwalker;
        private static Menu OrbwalkingMenu;
        private static Menu TSMenu;

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

            //init menu
            Men = new Menu("Katarina", "Katarina", true);
            Men.AddToMainMenu();
            OrbwalkingMenu = new Menu("Orb Walker", "Orb Walker");
            Orbwalker = new Orbwalking.Orbwalker(OrbwalkingMenu);
            Men.AddSubMenu(OrbwalkingMenu);
            // ts menu
            TSMenu = new Menu("Target Selector", "Target Selector");
            TargetSelector.AddToMenu(TSMenu);
            Men.AddSubMenu(TSMenu);
            //drawing menu
            Men.AddSubMenu(new Menu("Drawing", "Drawing"));
            Men.SubMenu("Drawing").AddItem(new MenuItem("Range circle", "Range circle").SetValue(false));
            Men.SubMenu("Drawing").AddItem(new MenuItem("HP indicator", "HP indicator (broken)").SetValue(false));
            //farming settings
            Men.AddSubMenu(new Menu("Harass", "Harass"));
            Men.SubMenu("Harass").AddItem(new MenuItem("Mode", "Mode").SetValue(new StringList(new[] { "QEW", "QW" }, 0)));

            LeagueSharp.Drawing.OnDraw += Drawing;
            LeagueSharp.Obj_AI_Base.OnPlayAnimation += PlayAnimation;
            Game.OnUpdate += Tick;

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
                KillSteal();
                Combo();
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
                    var health = hero.Health;
                    var maxhealth = hero.MaxHealth;
                    Vector2 pos = hero.HPBarPosition;

                    // 34 x 9 y hpbar start
                    // 138 x end
                    var percent = health / maxhealth * 104f;
                    Vector2 start = new Vector2(34f, 8f);
                    Vector2 end = new Vector2(34f + percent, 8f);
                    LeagueSharp.Drawing.DrawLine(hero.HPBarPosition + start, hero.HPBarPosition + end, 1f, System.Drawing.Color.Red);
                }
            }
        }

        private static bool InIgniteRange(Obj_AI_Hero hero)
        {
            if (hero.Distance(Player.ServerPosition) <= 600) return true;
            else return false;
        }

        private static void KillSteal()
        {
            foreach (Obj_AI_Hero hero in ObjectManager.Get<Obj_AI_Hero>().Where(
                    hero =>
                    ObjectManager.Player.Distance(hero.ServerPosition) <= E.Range
                    && !hero.IsMe
                    && hero.IsValidTarget()
                    && hero.IsEnemy
                    && !hero.IsInvulnerable
                ))
            {
                var Qdmg = Q.GetDamage(hero) * overkill;
                var Wdmg = W.GetDamage(hero) * overkill;
                var Edmg = E.GetDamage(hero) * overkill;
                var MarkDmg = Damage.CalcDamage(Player, hero, Damage.DamageType.Magical, Player.FlatMagicDamageMod * 0.15 + Player.Level * 15) * overkill;
                float Ignitedmg;
                if (IgniteSlot != SpellSlot.Unknown)
                {
                    Ignitedmg = (float)Damage.GetSummonerSpellDamage(Player, hero, Damage.SummonerSpell.Ignite);
                }
                else { Ignitedmg = 0f; }
                //procmark ks
                if (hero.HasBuff("KatarinaQMark") && hero.Health - Wdmg - MarkDmg < 0 && W.IsReady() && W.IsInRange(hero))
                {
                    W.Cast();
                    return;
                }
                // ignite
                if (hero.Health - Ignitedmg < 0 && IgniteSlot.IsReady() && InIgniteRange(hero))
                {
                    Player.Spellbook.CastSpell(IgniteSlot, hero);
                    return;
                }
                // E
                if (hero.Health - Edmg < 0 && E.IsReady() && E.IsInRange(hero))
                {
                    E.Cast(hero);
                    return;
                }
                // Q
                if (hero.Health - Qdmg < 0 && Q.IsReady() && Q.IsInRange(hero))
                {
                    Q.Cast(hero);
                    return;
                }
                if (E.IsInRange(hero))
                {
                    // E+W
                    if (hero.Health - Edmg - Wdmg < 0 && E.IsReady() && W.IsReady())
                    {
                        E.Cast(hero);
                        W.Cast();
                        return;
                    }
                    // E+Q
                    if (hero.Health - Edmg - Qdmg < 0 && E.IsReady() && Q.IsReady())
                    {
                        E.Cast(hero);
                        Q.Cast(hero);
                        return;
                    }
                    // E + Q + W no proc Q
                    
                    if (hero.Health - Edmg - Wdmg - Qdmg < 0 && E.IsReady() && Q.IsReady() && W.IsReady())
                    {
                        E.Cast(hero);
                        Q.Cast(hero);
                        W.Cast();
                        return;
                        // cast w without procing mark
                    }
                    
                    // E+Q+W+proc
                    if (hero.Health - Edmg - Wdmg - Qdmg - MarkDmg < 0 && E.IsReady() && Q.IsReady() && W.IsReady())
                    {
                        E.Cast(hero);
                        Q.Cast(hero);
                        return;
                        //W.Cast();
                        // no need to cast W because if it casts too fast no mark proc, so it waits until another func casts it
                    }
                    // E+Q+W+ignite
                    if (hero.Health - Edmg - Wdmg - Qdmg - Ignitedmg < 0 && E.IsReady() && Q.IsReady() && W.IsReady() && IgniteSlot.IsReady())
                    {
                        E.Cast(hero);
                        Q.Cast(hero);
                        W.Cast();
                        Player.Spellbook.CastSpell(IgniteSlot, hero);
                        return;
                    }
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
                    && !focus.IsMe
                    && !focus.IsInvulnerable
                    && focus.IsValidTarget()
                ))
                {
                    var Qdmg = Q.GetDamage(focus) * overkill;
                    var Wdmg = W.GetDamage(focus) * overkill;
                    var MarkDmg = Damage.CalcDamage(Player, focus, Damage.DamageType.Magical, Player.FlatMagicDamageMod * 0.15 + Player.Level * 15) * overkill;
                    float Ignitedmg;
                    if (IgniteSlot != SpellSlot.Unknown)
                    {
                        Ignitedmg = (float)Damage.GetSummonerSpellDamage(Player, focus, Damage.SummonerSpell.Ignite);
                    }
                    else { Ignitedmg = 0f; }
                    

                    // Q
                    if (focus.Health - Qdmg < 0 && E.IsReady() && Q.IsReady() && focus.Distance(target.ServerPosition) <= Q.Range)
                    {
                        E.Cast(target);
                        Q.Cast(focus);
                    }
                    // Q+W
                    if (focus.Distance(target.ServerPosition) <= W.Range && focus.Health - Qdmg - Wdmg < 0 && E.IsReady() && Q.IsReady())
                    {
                        E.Cast(target);
                        Q.Cast(focus);
                        W.Cast();
                    }

                    // Q + ignite
                    if (focus.Distance(target.ServerPosition) <= E.Range && focus.Health - Qdmg - Ignitedmg < 0 && E.IsReady() && Q.IsReady() && IgniteSlot.IsReady())
                    {
                        E.Cast(target);
                        Q.Cast(focus);
                        Player.Spellbook.CastSpell(IgniteSlot, focus);
                    }
                    // Q + W + markproc
                    if (focus.Distance(target.ServerPosition) <= W.Range && focus.Health - Qdmg - Wdmg - MarkDmg < 0 && E.IsReady() && Q.IsReady() && W.IsReady())
                    {
                        E.Cast(target);
                        Q.Cast(focus);
                        //mark procs auto in ks func no need to cast
                    }
                    // Q + W + ignite
                    if (focus.Distance(target.ServerPosition) <= W.Range && focus.Health - Qdmg - Wdmg - Ignitedmg < 0 && E.IsReady() && Q.IsReady() && W.IsReady() && IgniteSlot.IsReady())
                    {
                        E.Cast(target);
                        Q.Cast(focus);
                        W.Cast();
                        Player.Spellbook.CastSpell(IgniteSlot, focus);
                        return;
                    }
                    // Q + W + markproc + ignite
                    if (focus.Distance(target.ServerPosition) <= W.Range && focus.Health - Qdmg - Wdmg - MarkDmg - Ignitedmg < 0 && E.IsReady() && Q.IsReady() && W.IsReady())
                    {
                        E.Cast(target);
                        Q.Cast(focus);
                        Player.Spellbook.CastSpell(IgniteSlot, focus);
                        //mark procs auto in ks func no need to cast
                        return;
                    }

                }
            }

        }

        private static void Combo()
        {
            Obj_AI_Hero Target = TargetSelector.GetTarget(E.Range, TargetSelector.DamageType.Magical);

            if (Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.Combo && Target.IsValidTarget() && !InUlt)
            {
                if (Target.HasBuff("KatarinaQMark") && W.IsReady() && W.IsInRange(Target))
                {
                    W.Cast();
                }
                if (Q.IsInRange(Target))
                {
                    if (Q.IsReady())
                    {
                        Q.Cast(Target);
                    }
                    if (E.IsReady())
                    {
                        E.Cast(Target);
                    }
                }
                else
                {
                    if (E.IsReady())
                    {
                        E.Cast(Target);
                    }
                    if (Q.IsReady())
                    {
                        Q.Cast(Target);
                    }
                }
                /*
                if (W.IsReady() && W.IsInRange(Target))
                {
                    W.Cast();
                }
                */
                if (R.IsReady() && !InUlt && !E.IsReady())
                {
                    Orbwalker.SetAttack(false);
                    Orbwalker.SetMovement(false);
                    R.Cast();
                    InUlt = true;
                    return;
                }
            }
            if (Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.Mixed && Target.IsValidTarget() && !InUlt)
            {
                if (Target.HasBuff("KatarinaQMark") && W.IsReady() && W.IsInRange(Target))
                {
                    W.Cast();
                }
                if (Q.IsInRange(Target))
                {
                    if (Q.IsReady())
                    {
                        Q.Cast(Target);
                    }
                    if (E.IsReady() && Men.SubMenu("Harass").Item("Mode").GetValue<StringList>().SelectedIndex == 0)
                    {
                        E.Cast(Target);
                    }
                }
                else
                {
                    if (E.IsReady() && Men.SubMenu("Harass").Item("Mode").GetValue<StringList>().SelectedIndex == 0)
                    {
                        E.Cast(Target);
                    }
                    if (Q.IsReady())
                    {
                        Q.Cast(Target);
                    }
                }
                /*
                if (W.IsReady() && Player.Distance(Target.ServerPosition) < W.Range) //&& Target.HasBuff("katarinaqmark")
                {
                    W.Cast();
                }
                */
            }

        }

        private static void Farm()
        {

            if (Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.LastHit || Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.LaneClear)
            {
                foreach (var minion in ObjectManager.Get<Obj_AI_Minion>().Where(minion => minion.IsValidTarget() && minion.IsEnemy && minion.Distance(Player.ServerPosition) < E.Range))
                {
                    var Qdmg = Q.GetDamage(minion);
                    var Wdmg = W.GetDamage(minion);
                    var MarkDmg = Damage.CalcDamage(Player, minion, Damage.DamageType.Magical, Player.FlatMagicDamageMod * 0.15 + Player.Level * 15);

                    if (Player.Distance(minion.ServerPosition) <= Meleerng && Player.CalcDamage(minion, Damage.DamageType.Physical, Player.BaseAttackDamage) > minion.Health)
                    {
                        return;
                    }

                    if (minion.Health - Qdmg <= 0 && minion.Distance(Player.ServerPosition) <= Q.Range && Q.IsReady()) { Q.Cast(minion); }
                    if (minion.Health - Wdmg <= 0 && minion.Distance(Player.ServerPosition) <= W.Range && W.IsReady()) { W.Cast(); }
                    if (minion.Health - Wdmg - Qdmg <= 0 && minion.Distance(Player.ServerPosition) <= W.Range && Q.IsReady() && W.IsReady()) { Q.Cast(minion); W.Cast(); }
                    if (minion.HasBuff("KatarinaQMark") && minion.Health - Wdmg - MarkDmg <= 0 && minion.Distance(Player.ServerPosition) <= W.Range && W.IsReady())
                    {
                        W.Cast();
                    }
                    if (minion.Health - Wdmg - Qdmg - MarkDmg <= 0 && minion.Distance(Player.ServerPosition) <= W.Range && Q.IsReady() && W.IsReady())
                    {
                        Q.Cast(minion);
                    }

                }
            }
            if (Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.LaneClear)
            {
                foreach (var minion in ObjectManager.Get<Obj_AI_Minion>().Where(minion => minion.IsValidTarget() && minion.IsEnemy && minion.Distance(Player.ServerPosition) < E.Range))
                {
                    if (Player.Distance(minion.ServerPosition) <= Meleerng && Player.CalcDamage(minion, Damage.DamageType.Physical, Player.BaseAttackDamage) > minion.Health)
                    {
                        return;
                    }
                    var Qdmg = Q.GetDamage(minion);
                    var Wdmg = W.GetDamage(minion);
                    var Edmg = E.GetDamage(minion);
                    var MarkDmg = Damage.CalcDamage(Player, minion, Damage.DamageType.Magical, Player.FlatMagicDamageMod * 0.15 + Player.Level * 15);

                    if (minion.Health - Edmg <= 0 && minion.Distance(Player.ServerPosition) <= E.Range && E.IsReady()) { E.Cast(minion); }

                    if (minion.Health - Wdmg - Qdmg - MarkDmg - Edmg <= 0 && minion.Distance(Player.ServerPosition) <= W.Range && E.IsReady() && Q.IsReady() && W.IsReady())
                    {
                        E.Cast(minion);
                        Q.Cast(minion);
                    }

                }
            }
        }

    }
}
