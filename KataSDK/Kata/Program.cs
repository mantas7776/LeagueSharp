using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using LeagueSharp;
using LeagueSharp.SDK.Core;
using LeagueSharp.SDK.Core.Wrappers;
using LeagueSharp.SDK.Core.Enumerations;
using LeagueSharp.SDK.Core.Extensions;
using LeagueSharp.SDK.Core.UI.IMenu;
using LeagueSharp.SDK.Core.UI.IMenu.Values;
using SharpDX;
using LeagueSharp.SDK.Core.Events;


namespace KataSDK
{
    class Program
    {
        #region Variables
        //shortcuts
        private static readonly Obj_AI_Hero Player = ObjectManager.Player;
        // spells && items
        private static Spell Q;
        private static Spell W;
        private static Spell E;
        private static Spell R;
        private static bool InUlt = false;
        private static SpellSlot IgniteSlot;
        private const int Meleerng = 125;
        private const float overkill = 15f;

        //menu & orbwalking
        private static Menu Men;
        #endregion

        static void Main(string[] args)
        {
            Load.OnLoad += Game_OnGameLoad;
        }

        #region GameLoad
        private static void Game_OnGameLoad(object sender, EventArgs e)
        {
            if (Player.ChampionName != "Katarina") return;
            Bootstrap.Init(null);
            Game.PrintChat("Simple katarina is loading...");
            Q = new Spell(SpellSlot.Q, 675f);
            W = new Spell(SpellSlot.W, 375f);
            E = new Spell(SpellSlot.E, 700f);
            R = new Spell(SpellSlot.R, 550f);
            IgniteSlot = ObjectManager.Player.GetSpellSlot("SummonerDot");

            //init menu
            Men = new Menu("Katarina", "Katarina", true);
            //drawing menu
            Menu DrawingMenu = new Menu("Drawing", "Drawing");
            {
                DrawingMenu.Add(new MenuBool("Range circle", "Range circle", false));
                DrawingMenu.Add(new MenuBool("HP indicator", "HP indicator", false));
                Men.Add(DrawingMenu);
            }
            //farming settings
            Menu Harass = new Menu("Harass", "Harass");
            //Men.SubMenu("Harass").AddItem(new MenuItem("Mode", "Mode").SetValue(new StringList(new[] { "QEW", "QW" }, 0)));
            MenuList MenuMode = new MenuList<string>("Mode", "Mode", new[] { "QEW", "QW" });
            Harass.Add(MenuMode);
            Men.Add(Harass);
            Men.Attach();

            Drawing.OnDraw += OnDrawing;
            Obj_AI_Base.OnPlayAnimation += PlayAnimation;
            Game.OnUpdate += Tick;

        }
        #endregion
        #region Tick
        private static void Tick(EventArgs args)
        {
            if (ObjectManager.Player.IsDead) return;
            if (InUlt)
            {
                Orbwalker.Attack = false;
                Orbwalker.Movement = false;
                return;
            }
            else
            {
                KillSteal();
                if (Q.Level == 0 || W.Level == 0 || E.Level == 0) SimpleCombo();
                else Combo();
                Farm();
                Orbwalker.Attack = true;
                Orbwalker.Movement = true;
            }
        }
        #endregion
        #region Ult detection
        private static void PlayAnimation(GameObject sender, GameObjectPlayAnimationEventArgs args)
        {
            if (sender.IsMe)
            {
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
        #endregion
        #region Drawing
        private static void OnDrawing(EventArgs args)
        {
            if (Men["Drawing"]["Range circle"].GetValue<MenuBool>().Value == true)
            {
                Drawing.DrawCircle(ObjectManager.Player.Position, E.Range, System.Drawing.Color.LimeGreen);
            }

            if (Men["Drawing"]["HP indicator"].GetValue<MenuBool>().Value == true)
            {
                foreach (var hero in ObjectManager.Get<Obj_AI_Hero>().Where(hero => hero.IsValidTarget() && hero.IsEnemy))
                {
                    var health = hero.Health;
                    var maxhealth = hero.MaxHealth;
                    Vector2 pos = hero.HPBarPosition;
                    pos.Y += 20;
                    pos.X += 9;
                    Vector2 pos2 = pos;
                    var currenthp = hero.Health * 104f / hero.MaxHealth;
                    pos.X += currenthp;
                   // Console.WriteLine("Curr: " + currenthp);
                    var aftercombo = CalculateDmg(hero);
                    pos2.X += aftercombo;
                    if (aftercombo <= currenthp - 1)
                    {
                        Drawing.DrawLine(pos, pos2, 1, System.Drawing.Color.Aqua);
                    }
                }
            }
        }

        private static float CalculateDmg(Obj_AI_Hero hero)
        {
            float lefthp = hero.Health;
            if (Q.IsReady()) lefthp -= (float)Qdmg(hero);
            if (E.IsReady()) lefthp -= (float)Edmg(hero);
            if (W.IsReady()) lefthp -= (float)Wdmg(hero);
            if (R.IsReady()) lefthp -= (float)RDmg(hero);
            lefthp -= (float)MarkDmg(hero);
            //Console.WriteLine(lefthp);
            lefthp = lefthp * 104f / hero.MaxHealth;
            return lefthp;
        }

        #endregion

        private static bool InIgniteRange(Obj_AI_Hero hero)
        {
            if (hero.Distance(Player.ServerPosition) <= 600) return true;
            else return false;
        }
        #region Killsteal
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
                if (hero == null) return;
                //procmark ks
                if (hero.HasBuff("KatarinaQMark") && hero.Health - Wdmg(hero) - MarkDmg(hero) < 0 && W.IsReady() && W.IsInRange(hero))
                {
                    W.Cast();
                    return;
                }
                // ignite
                if (hero.Health - IgniteDmg(hero) < 0 && IgniteSlot.IsReady() && InIgniteRange(hero))
                {
                    Player.Spellbook.CastSpell(IgniteSlot, hero);
                }
                // E
                if (hero.Health - Edmg(hero) < 0 && E.IsReady() && E.IsInRange(hero))
                {
                    E.Cast(hero);
                }
                // Q
                if (hero.Health - Qdmg(hero) < 0 && Q.IsReady() && Q.IsInRange(hero))
                {
                    Q.Cast(hero);
                }
                if (E.IsInRange(hero))
                {
                    // E+W
                    if (hero.Health - Edmg(hero) - Wdmg(hero) < 0 && E.IsReady() && W.IsReady())
                    {
                        E.Cast(hero);
                        W.Cast();
                    }
                    // E+Q
                    if (hero.Health - Edmg(hero) - Qdmg(hero) < 0 && E.IsReady() && Q.IsReady())
                    {
                        E.Cast(hero);
                        Q.Cast(hero);
                    }
                    // E + Q + W no proc Q

                    if (hero.Health - Edmg(hero) - Wdmg(hero) - Qdmg(hero) < 0 && E.IsReady() && Q.IsReady() && W.IsReady())
                    {
                        E.Cast(hero);
                        Q.Cast(hero);
                        W.Cast();
                        // cast w without procing mark
                    }
                    
                    // E+Q+W+proc
                    if (hero.Health - Edmg(hero) - Wdmg(hero) - Qdmg(hero) - MarkDmg(hero) < 0 && E.IsReady() && Q.IsReady() && W.IsReady())
                    {
                        E.Cast(hero);
                        Q.Cast(hero);
                        return;
                        //W.Cast();
                        // no need to cast W because if it casts too fast no mark proc, so it waits until another func casts it
                    }
                    // E+Q+W+ignite
                    if (hero.Health - Edmg(hero) - Wdmg(hero) - Qdmg(hero) - IgniteDmg(hero) < 0 && E.IsReady() && Q.IsReady() && W.IsReady() && IgniteSlot.IsReady())
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
                    && target.IsValidTarget()
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
                    if (focus == null || target == null) return;
                    // Q
                    if (focus.Health - Qdmg(focus) < 0 && E.IsReady() && Q.IsReady() && focus.Distance(target.ServerPosition) <= Q.Range)
                    {
                        E.Cast(target);
                        Q.Cast(focus);
                    }
                    // Q+W
                    if (focus.Distance(target.ServerPosition) <= W.Range && focus.Health - Qdmg(focus) - Wdmg(focus) < 0 && E.IsReady() && Q.IsReady())
                    {
                        E.Cast(target);
                        Q.Cast(focus);
                        W.Cast();
                    }

                    // Q + ignite
                    if (focus.Distance(target.ServerPosition) <= E.Range && focus.Health - Qdmg(focus) - IgniteDmg(focus) < 0 && E.IsReady() && Q.IsReady() && IgniteSlot.IsReady())
                    {
                        E.Cast(target);
                        Q.Cast(focus);
                        Player.Spellbook.CastSpell(IgniteSlot, focus);
                    }
                    // Q + W + markproc
                    if (focus.Distance(target.ServerPosition) <= W.Range && focus.Health - Qdmg(focus) - Wdmg(focus) - MarkDmg(focus) < 0 && E.IsReady() && Q.IsReady() && W.IsReady())
                    {
                        E.Cast(target);
                        Q.Cast(focus);
                        //mark procs auto in ks func no need to cast
                    }
                    // Q + W + ignite
                    if (focus.Distance(target.ServerPosition) <= W.Range && focus.Health - Qdmg(focus) - Wdmg(focus) - IgniteDmg(focus) < 0 && E.IsReady() && Q.IsReady() && W.IsReady() && IgniteSlot.IsReady())
                    {
                        E.Cast(target);
                        Q.Cast(focus);
                        W.Cast();
                        Player.Spellbook.CastSpell(IgniteSlot, focus);
                        return;
                    }
                    // Q + W + markproc + ignite
                    if (focus.Distance(target.ServerPosition) <= W.Range && focus.Health - Qdmg(focus) - Wdmg(focus) - MarkDmg(focus) - IgniteDmg(focus) < 0 && E.IsReady() && Q.IsReady() && W.IsReady())
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
        #endregion

        #region Combo
        private static void SimpleCombo()
        {
            var Target = TargetSelector.GetTarget(E.Range, DamageType.Magical);
            if (Target == null) return;
            if (Orbwalker.ActiveMode == OrbwalkerMode.Orbwalk)
            {
                if (Q.IsReady()) Q.Cast(Target);
                if (E.IsReady()) E.Cast(Target);
            }
        }

        private static void Combo()
        {
            Obj_AI_Hero Target = TargetSelector.GetTarget(E.Range, DamageType.Magical);
            if (Target == null) return;

            if (Orbwalker.ActiveMode == OrbwalkerMode.Orbwalk && Target.IsValidTarget() && !InUlt)
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
                    if (!Q.IsReady() && !Qflying() && W.IsReady() && W.IsInRange(Target) && Q.LastCastAttemptT + 1000 < Game.Time * 1000)
                    {
                        W.Cast();
                    }
                }
                else
                {
                    if (E.IsInRange(Target))
                    {
                        if (E.IsReady())
                        {
                            E.Cast(Target);
                        }
                        if (Q.IsReady())
                        {
                            Q.Cast(Target);
                        }
                        if (!Q.IsReady() && !Qflying() && W.IsReady() && W.IsInRange(Target) && Q.LastCastAttemptT + 1000 < Game.Time * 1000)
                        {
                            W.Cast();
                        }
                    }
                }

                if (R.IsReady() && !InUlt && !E.IsReady())
                {
                    Orbwalker.Attack = false;
                    Orbwalker.Movement = false;
                    if (W.IsReady())
                    {
                        W.Cast();
                    }
                    R.Cast();
                    InUlt = true;
                    return;
                }
            }
            if (Orbwalker.ActiveMode == OrbwalkerMode.Hybrid && Target.IsValidTarget() && !InUlt)
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
                    if (E.IsReady() && Men["Harass"]["Mode"].GetValue<MenuList>().Index == 0)
                    {
                        E.Cast(Target);
                    }
                }
                else
                {
                    if (E.IsReady() && Men["Harass"]["Mode"].GetValue<MenuList>().Index == 0)
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
        #endregion

        #region Qflying
        private static bool Qflying()
        {
            if (ObjectManager.Get<MissileClient>().Where(missile => missile.SData.Name.ToLower() == "katarinaqmis" && missile.SpellCaster.IsMe).Count() > 0) return true;
            else return false;
        }
        #endregion
        #region SpellDamage
        private static double Qdmg(Obj_AI_Base target)
        {
            if (Q.Level == 0) return 0;
            return Player.CalculateDamage(target,DamageType.Magical,
                new[] { 60, 85, 110, 135, 160 }[Q.Level - 1] + 0.45 * Player.FlatMagicDamageMod) - overkill;
        }
        private static double Wdmg(Obj_AI_Base target)
        {
            if (W.Level == 0) return 0;
            return Player.CalculateDamage(target, DamageType.Magical,
                new[] { 40, 75, 110, 145, 180 }[W.Level - 1] + 0.25 * Player.FlatMagicDamageMod + (Player.TotalAttackDamage - Player.BaseAttackDamage) * 0.60) - overkill;
        }
        private static double Edmg(Obj_AI_Base target)
        {
            if (E.Level == 0) return 0;
            return Player.CalculateDamage(target, DamageType.Magical,
                new[] { 40, 70, 100, 130, 160 }[E.Level - 1] + 0.25 * Player.FlatMagicDamageMod) - overkill;
        }
        private static double MarkDmg(Obj_AI_Base target)
        {
            return Player.CalculateDamage(target, DamageType.Magical, Player.FlatMagicDamageMod * 0.15 + Player.Level * 15) - overkill;        
        }
        private static double RDmg(Obj_AI_Base target)
        {
            if (R.Level == 0) return 0;
            return Player.CalculateDamage(target, DamageType.Magical,
               new[] { 350, 550, 750 }[R.Level - 1] + 2.5 * Player.FlatMagicDamageMod + (Player.TotalAttackDamage - Player.BaseAttackDamage) * 3.75) - overkill;
        }
        private static double IgniteDmg(Obj_AI_Base target)
        {
            float Ignitedmg;
            if (IgniteSlot != SpellSlot.Unknown)
            {
                //Ignitedmg = (float)Damage.GetSpellDamage(Player, target, IgniteSlot);
                Ignitedmg = 0f;
            }
            else { Ignitedmg = 0f; }
            return Ignitedmg;
        }
        #endregion
        #region farm&laneclear
        private static void Farm()
        {

            if (Orbwalker.ActiveMode == OrbwalkerMode.LastHit || Orbwalker.ActiveMode == OrbwalkerMode.LaneClear)
            {
                foreach (var minion in ObjectManager.Get<Obj_AI_Minion>().Where(minion => minion.IsValidTarget() && minion.IsEnemy && minion.Distance(Player.ServerPosition) < E.Range))
                {
                    if (Player.Distance(minion.ServerPosition) <= Meleerng && Player.GetAutoAttackDamage(minion) > minion.Health)
                    {
                        return;
                    }

                    if (minion.Health - Qdmg(minion) <= 0 && minion.Distance(Player.ServerPosition) <= Q.Range && Q.IsReady()) { Q.Cast(minion); }
                    if (minion.Health - Wdmg(minion) <= 0 && minion.Distance(Player.ServerPosition) <= W.Range && W.IsReady()) { W.Cast(); }
                    if (minion.Health - Wdmg(minion) - Qdmg(minion) <= 0 && minion.Distance(Player.ServerPosition) <= W.Range && Q.IsReady() && W.IsReady()) { Q.Cast(minion); W.Cast(); }
                    if (minion.HasBuff("KatarinaQMark") && minion.Health - Wdmg(minion) - MarkDmg(minion) <= 0 && minion.Distance(Player.ServerPosition) <= W.Range && W.IsReady())
                    {
                        W.Cast();
                    }
                    if (minion.Health - Wdmg(minion) - Qdmg(minion) - MarkDmg(minion) <= 0 && minion.Distance(Player.ServerPosition) <= W.Range && Q.IsReady() && W.IsReady())
                    {
                        Q.Cast(minion);
                    }

                }
            }
            if (Orbwalker.ActiveMode == OrbwalkerMode.LaneClear)
            {
                foreach (var minion in ObjectManager.Get<Obj_AI_Minion>().Where(minion => minion.IsValidTarget() && minion.IsEnemy && minion.Distance(Player.ServerPosition) < E.Range))
                {
                    if (Player.Distance(minion.ServerPosition) <= Meleerng && Player.GetAutoAttackDamage(minion) > minion.Health)
                    {
                        return;
                    }
                    if (minion.Health - Edmg(minion) <= 0 && minion.Distance(Player.ServerPosition) <= E.Range && E.IsReady()) { E.Cast(minion); }

                    if (minion.Health - Wdmg(minion) - Qdmg(minion) - MarkDmg(minion) - Edmg(minion) <= 0 && minion.Distance(Player.ServerPosition) <= W.Range && E.IsReady() && Q.IsReady() && W.IsReady())
                    {
                        E.Cast(minion);
                        Q.Cast(minion);
                    }

                }
            }
        }
        #endregion

    }
}
