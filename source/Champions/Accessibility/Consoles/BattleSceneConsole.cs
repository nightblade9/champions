using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using DeenGames.Champions.Events;
using DeenGames.Champions.Models;
using DeenGames.Champions.Scenes;
using Puffin.Core.Events;

namespace DeenGames.Champions.Accessibility.Consoles
{
    /// <summary>
    /// Encapsulates the state and reads/writes commands to the console for the battle scene.
    /// </summary>
    public class BattleSceneConsole : IDisposable
    {
        private bool IsGamePaused = false;
        private Thread replThread;
        private bool isRunning = true;
        private List<Unit> party;
        private List<Unit> monsters;
        
        // Boxed int
        private BoxedInt numPotions;

        public BattleSceneConsole(List<Unit> party, List<Unit> monsters, BoxedInt numPotions)
        {
            // This is bad. Use an event bus instead.
            this.party = party;
            this.monsters = monsters;
            this.numPotions = numPotions;

            EventBus.LatestInstance.Subscribe(ChampionsEvent.PauseGame, (data) => this.IsGamePaused = true);
            EventBus.LatestInstance.Subscribe(ChampionsEvent.ResumeGame, (data) => this.IsGamePaused = false);
        }
        
        public void StartRepl()
        {
            this.replThread = new Thread(() => 
            {
                while (isRunning)
                {
                    Console.WriteLine("Your command? ");
                    var key = Console.ReadKey();
                    var lowerCaseKey = key.KeyChar.ToString().ToLower()[0];
                    var isShiftDown = key.Modifiers.HasFlag(ConsoleModifiers.Shift);
                    this.ProcessCommand(lowerCaseKey, isShiftDown);
                }
            });
            this.replThread.Start();
        }

        private void ProcessCommand(char input, bool isShiftDown)
        {
            if (isShiftDown)
            {
                EventBus.LatestInstance.Broadcast(ChampionsEvent.PauseGame);
            }

            if (input == 'q') {
                this.PrintStats(this.monsters[0]);
            } else if (input == 'w') {
                this.PrintStats(this.monsters[1]);
            } else if (input == 'e') {
                this.PrintStats(this.monsters[2]);
            } else if (input == 'r') {
                this.PrintStats(this.monsters[3]);
            } else if (input == 't') {
                this.PrintStats(this.monsters[4]);
            }
            
            else if (input == 'a') {
                if (isShiftDown) {
                    EventBus.LatestInstance.Broadcast(ChampionsEvent.UsePotion, this.party[0]);
                } else {
                    this.PrintStats(this.party[0]);
                }
            } else if (input == 's') {
                if (isShiftDown) {
                    EventBus.LatestInstance.Broadcast(ChampionsEvent.UsePotion, this.party[1]);
                } else {
                    this.PrintStats(this.party[1]);
                }
            } else if (input == 'd') {
                if (isShiftDown) {
                    EventBus.LatestInstance.Broadcast(ChampionsEvent.UsePotion, this.party[2]);
                } else {
                    this.PrintStats(this.party[2]);
                }
            } else if (input == 'f') {
                if (isShiftDown) {
                    EventBus.LatestInstance.Broadcast(ChampionsEvent.UsePotion, this.party[3]);
                } else {
                    this.PrintStats(this.party[3]);
                }
            } else if (input == 'g') {
                if (isShiftDown) {
                    EventBus.LatestInstance.Broadcast(ChampionsEvent.UsePotion, this.party[4]);
                } else {
                    this.PrintStats(this.party[4]);
                }
            }

            else if (input == 'x')
            {
                Console.WriteLine("Bye!");
                Environment.Exit(0);
            }
            else if (input == 'h')
            {
                EventBus.LatestInstance.Broadcast(ChampionsEvent.PauseGame);
                Console.WriteLine("Commands: h for help, i for inventory, p to pause, o to use a potion, g for info, x to quit. Q W E R T to check monster stats, A S D F G to check party member stats.");
                Thread.Sleep(3000);
                EventBus.LatestInstance.Broadcast(ChampionsEvent.ResumeGame);
            }
            else if (input ==  'i')
            {
                Console.WriteLine($"Inventory: {numPotions.Value} potions");
            }
            else if (input == 'p')
            {
                if (this.IsGamePaused)
                {
                    Console.WriteLine("Unpaused. Press p to pause.");
                    EventBus.LatestInstance.Broadcast(ChampionsEvent.ResumeGame);
                } else {
                    Console.WriteLine("Paused. Press p to unpause.");
                    EventBus.LatestInstance.Broadcast(ChampionsEvent.PauseGame);
                }
            }
            else if (input == 'o') // use potion
            {
                if (numPotions.Value == 0)
                {
                    Console.WriteLine("You're out of potions.");
                }
                else
                {
                    EventBus.LatestInstance.Broadcast(ChampionsEvent.PauseGame);

                    Console.WriteLine("Press the number for the party member to use it on.");
                    var alive = this.party.Where(p => p.CurrentHealth > 0);
                    foreach (var i in Enumerable.Range(0, alive.Count()))
                    {
                        var member = alive.ElementAt(i);
                        Console.WriteLine($"{i}: {member.Name}, {member.CurrentHealth} out of {member.TotalHealth} health");
                    }
                    
                    var partyNum = -1;
                    while (!int.TryParse(Console.ReadKey().KeyChar.ToString(), out partyNum))
                    {
                        Console.WriteLine($"Try again - enter a number from 0 to {alive.Count() - 1}");
                    }

                    // Also bad: game logic shouldn't live here
                    var target = alive.ElementAt(partyNum);

                    EventBus.LatestInstance.Broadcast(ChampionsEvent.UsePotion, target);
                }
            }
            else if (input == 'g')
            {
                this.StateParties(true);
            }
        }

        public void Dispose()
        {
            this.isRunning = false;
            this.replThread.Join();
        }

        internal void PrintStats(Unit unit)
        {
            Console.WriteLine($"{unit.Name} has {unit.CurrentHealth} out of {unit.TotalHealth} health");
        }

        internal void StateParties(bool stateHealth = false)
        {
            EventBus.LatestInstance.Broadcast(ChampionsEvent.PauseGame);

            StringBuilder partyText = new StringBuilder();
            
            partyText.Append("Your party is: ");
            foreach (var member in party)
            {
                var health = "";
                if (stateHealth)
                {
                    health = $" with {member.CurrentHealth} out of {member.TotalHealth} health";
                }
                partyText.Append($"A level {member.Level} {member.Specialization} {health}, ");
            }
            
            partyText.Append($"They are facing: {this.monsters.Count} slimes.");
            if (stateHealth)
            {
                partyText.Append(" Their health is ");
                foreach (var slime in monsters)
                {
                    partyText.Append($"{slime.Name}: {slime.CurrentHealth} out of {slime.TotalHealth}, ");
                }
            }
            partyText.Append('.');
            Console.WriteLine(partyText.ToString());

            Thread.Sleep(5000);
            EventBus.LatestInstance.Broadcast(ChampionsEvent.ResumeGame);
        }

        internal void Print(string message)
        {
            Console.WriteLine(message);
        }
    }
}