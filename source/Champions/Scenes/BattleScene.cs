using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using DeenGames.Champions.Accessibility;
using DeenGames.Champions.Accessibility.Consoles;
using DeenGames.Champions.Events;
using DeenGames.Champions.Models;
using Puffin.Core;
using Puffin.Core.Ecs;
using Puffin.Core.Ecs.Components;
using Puffin.Core.Events;

namespace DeenGames.Champions.Scenes
{
    public class BattleScene : Scene
    {
        // Pause
        public bool IsActive { get; set; } = true;
        private bool IsComplete { get; set; } = false;

        private readonly int PLAYER_X = ChampionsGame.GAME_WIDTH - MONSTERS_X - Constants.IMAGE_SIZE;
        private const int MONSTERS_X = 300;
        
        // Level 10 for a medium-intelligence creature
        private const int ALWAYS_TARGET_WEAKEST_AT_INTELLIGENCE = 200;

        private readonly TimeSpan DELAY_BETWEEN_ACTIONS = TimeSpan.FromSeconds(1);
        private DateTime lastActionTime;
        private List<Unit> turns = new List<Unit>();

        private List<Unit> party;
        private List<Unit> monsters;
        private Random random = new Random();
        private Entity partyArrow;
        private Entity monsterArrow;
        private Entity news;

        /// Audios
        private Entity deathRattle;
        private Dictionary<Specialization, Entity> audios = new Dictionary<Specialization, Entity>();
        /// End audios

        // Poor man's MVVM: map of model => view-model
        private IDictionary<Unit, Entity> battleEntities = new Dictionary<Unit, Entity>();

        private BoxedInt numPotions = new BoxedInt(5);
        private BattleSceneConsole console;

        public BattleScene(List<Unit> party) : base()
        {
            this.party = party;
            var random = new Random();
            this.monsters = new List<Unit>()
            {
                new Unit(1, Specialization.Slime, random.Next(1, 4)),
                new Unit(2, Specialization.Slime, random.Next(1, 4)),
                new Unit(3, Specialization.Slime, random.Next(1, 4)),
                new Unit(4, Specialization.Slime, random.Next(1, 4)),
                new Unit(5, Specialization.Slime, random.Next(1, 4)),
            };

            console = new BattleSceneConsole(party, monsters, numPotions);

            this.LoadSounds();

            // Events
            EventBus.LatestInstance.Subscribe(ChampionsEvent.UsePotion, (target) => this.UsePotionOn(target as Unit));
            EventBus.LatestInstance.Subscribe(ChampionsEvent.PauseGame, (data) => this.IsActive = false);
            EventBus.LatestInstance.Subscribe(ChampionsEvent.ResumeGame, (data) => this.IsActive = true);
            EventBus.LatestInstance.Subscribe(ChampionsEvent.OnAttackOrSkill, (message) => {
                news.Get<TextLabelComponent>().Text = message.ToString();
                console.Print(message.ToString());
            });

            // Grass?
            this.BackgroundColour = 0x3c5956;

            partyArrow = new Entity().Move(PLAYER_X - (Constants.IMAGE_SIZE), 200)
                .Sprite(Path.Combine("Content", "Images", "Arrow-Right.png"));
            partyArrow.Get<SpriteComponent>().IsVisible = false;
            this.Add(partyArrow);

            // Extra +1 on IMAGE_SIZE to position on RHS
            monsterArrow = new Entity().Move(MONSTERS_X + (2 * Constants.IMAGE_SIZE), 200)
                .Sprite(Path.Combine("Content", "Images", "Arrow-Left.png"));
            this.Add(monsterArrow);

            news = new Entity().Move(300, 100).Label("");
            this.Add(news);

            
            for (var i = 0; i < this.party.Count; i++)
            {
                var unit = this.party[i];
                var entity = new Entity()
                    .Spritesheet(Constants.SpecializationsImageFile, Constants.IMAGE_SIZE, Constants.IMAGE_SIZE, (int)unit.Specialization)
                    .Label($"HP: {unit.CurrentHealth}/{unit.TotalHealth}", -30, -24);
                this.battleEntities[unit] = entity;
                this.Add(entity);
            }

            for (var i = 0; i < this.monsters.Count; i++)
            {
                var unit = this.monsters[i];
                var entity = new Entity()
                    .Spritesheet(Constants.SpecializationsImageFile, Constants.IMAGE_SIZE, Constants.IMAGE_SIZE, (int)unit.Specialization)
                    .Label($"HP: {unit.CurrentHealth}/{unit.TotalHealth}", -30, -24);
                this.battleEntities[unit] = entity;
                this.Add(entity);
            }

            this.ResetPositions();

            this.lastActionTime = DateTime.Now;
            
            this.console.Print("The battle begins! h for help, p to pause!");
            Thread.Sleep(1000);
        }
        
        override public void Ready()
        {
            base.Ready();
            this.console.StartRepl();
        }

        private void LoadSounds()
        {
            deathRattle = new Entity().Audio(Path.Combine("Content", "Audio", "Died.wav"));
            Add(deathRattle);

            foreach (Specialization s in (Specialization[]) Enum.GetValues(typeof(Specialization)))
            {
                this.audios[s] = new Entity().Audio(Path.Combine("Content", "Audio", $"{s}.wav"));
                Add(this.audios[s]);
            }
        }

        private void UsePotionOn(Unit target)
        {
            this.numPotions.Value -= 1;
            var healed = (int)Math.Ceiling(Constants.HEAL_POTION_PERCENT * target.TotalHealth);
            target.CurrentHealth = Math.Min(target.TotalHealth, target.CurrentHealth + healed);
            Console.WriteLine($"Healed {healed} HP, {target.Name} now has {target.CurrentHealth} out of {target.TotalHealth} health.");
            Thread.Sleep(2000);
            this.IsActive = true;
        }

        override public void Update(int elapsedMilliseconds)
        {
            if (!this.IsActive || this.IsComplete)
            {
                lastActionTime = DateTime.Now; // Pretend time is frozen
                return;
            }

            var now = DateTime.Now;
            if ((now - lastActionTime) < DELAY_BETWEEN_ACTIONS)
            {
                // Not yet time to do something else
                return;
            }

            if (!turns.Any())
            {
                // Generate a new round of turns
                // TODO: extract to a class and more turns if more speed
                this.turns = GenerateRoundOfTurns();
            }

            var next = this.turns.First();
            this.turns.RemoveAt(0);

            this.ExecuteTurn(next);
            this.lastActionTime = now;
        }

        private void ExecuteTurn(Unit next)
        {
            if (IsActive && !IsComplete)
            {
                var isPartysTurn = this.party.Contains(next);

                // TODO: AI based on level, etc.
                // Random target. TODO: intelligently target ... weakest? strongest? etc.
                Unit target = this.PickTargetFor(next, isPartysTurn);

                this.RepositionUnits(isPartysTurn, next, target);
                
                // Basic attack. TODO: intelligently pick a move.
                if (random.NextDouble() <= next.SkillProbability)
                {
                    next.UseSkill(this.party, this.monsters);
                    // Dunno who it hurt/healed
                    foreach (var entity in this.battleEntities.Keys)
                    {
                        battleEntities[target].Get<TextLabelComponent>().Text = $"HP: {entity.CurrentHealth}/{entity.TotalHealth}";
                    }
                }
                else
                {
                    next.Attack(target);
                    this.battleEntities[target].Get<TextLabelComponent>().Text = $"HP: {target.CurrentHealth}/{target.TotalHealth}";                   
                }

                this.CheckForGameOver(target);

                this.audios[next.Specialization].Get<AudioComponent>().Play();
                if (target.CurrentHealth <= 0)
                {
                    Thread.Sleep(500);
                    deathRattle.Get<AudioComponent>().Play();
                }
            }
        }

        private Unit PickTargetFor(Unit next, bool isPartysTurn)
        {
            var targets = isPartysTurn ? this.monsters : this.party;
            targets = targets.Where(t => t.CurrentHealth > 0).ToList();

            var targetWeakest = next.Intelligence <= random.Next(ALWAYS_TARGET_WEAKEST_AT_INTELLIGENCE);
            if (targetWeakest)
            {
                var minHealth = targets.Min(t => t.CurrentHealth);
                return targets.First(t => t.CurrentHealth == minHealth);
            }
            else
            {
                return targets[random.Next(targets.Count)];
            }
        }

        private void RepositionUnits(bool isPartysTurn, Unit next, Unit target)
        {
            if (isPartysTurn)
            {
                this.partyArrow.Get<SpriteComponent>().IsVisible = false;
                this.monsterArrow.Get<SpriteComponent>().IsVisible = true;
                this.monsterArrow.Y = this.battleEntities[target].Y;
            }
            else
            {
                this.monsterArrow.Y = this.battleEntities[next].Y;
                this.monsterArrow.Get<SpriteComponent>().IsVisible = false;
                this.partyArrow.Get<SpriteComponent>().IsVisible = true;
                this.partyArrow.Y = this.battleEntities[target].Y;
            }

            this.ResetPositions();

            // Acting unit stands in front of the rest
            this.battleEntities[next].X += isPartysTurn ? -Constants.IMAGE_SIZE : Constants.IMAGE_SIZE;
        }

        private void ResetPositions()
        {
            for (var i = 0; i < this.party.Count; i++)
            {
                var unit = this.party[i];
                this.battleEntities[unit].Move(PLAYER_X, 200 + (int)(i * Constants.IMAGE_SIZE * 2));
            }

            for (var i = 0; i < this.monsters.Count; i++)
            {
                var unit = this.monsters[i];
                this.battleEntities[unit].Move(MONSTERS_X, 200 + (int)(i * Constants.IMAGE_SIZE * 2));
            }
        }
        
        private void CheckForGameOver(Unit target)
        {
            if (this.monsters.All(m => m.CurrentHealth <= 0))
            {
                // VICTORY~!
                this.IsComplete = true;
                this.console.Print("You have won the battle!");
            }
            else if (this.party.All(p => p.CurrentHealth <= 0))
            {
                // Defeat! :(
                this.IsComplete = true;
                this.console.Print("You have lost the battle!");
            }
        }

        private List<Unit> GenerateRoundOfTurns()
        {
            // Simple: players first, speed-descending; then monsters first, speed-descending
            var turns = this.party.Where(p => p.CurrentHealth > 0).OrderByDescending(u => u.Speed).ToList();
            turns.AddRange(this.monsters.Where(m => m.CurrentHealth > 0).OrderByDescending(u => u.Speed));
            return turns;
        }
    }
}