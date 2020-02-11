using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using DeenGames.Champions.Models;
using DeenGames.Champions.Scenes;

namespace DeenGames.Champions.Accessibility.Consoles
{
    /// <summary>
    /// Encapsulates the state and reads/writes commands to the console for the battle scene.
    /// </summary>
    public class BattleSceneConsole : IDisposable
    {
        private Thread replThread;
        private bool isRunning = true;
        private List<Unit> party;
        private List<Unit> monsters;
        private BattleScene scene;
        
        // Boxed int
        private BoxedInt numPotions;

        public BattleSceneConsole(BattleScene scene, BoxedInt numPotions)
        {
            // This is bad. FIgure out another way to share.
            this.scene = scene;
            this.numPotions = numPotions;

            this.replThread = new Thread(() => 
            {
                while (isRunning)
                {
                    Console.WriteLine("Your command? ");
                    var text = Console.ReadLine();
                    this.ProcessCommand(text);
                }
            });
            this.replThread.Start();
        }

        private void ProcessCommand(string text)
        {
            if (text == "help" || text == "h")
            {
                Console.WriteLine("Commands: h for help, i for inventory, p to use a potion");
            }
            else if (text == "i" || text == "inv")
            {
                Console.WriteLine($"Inventory: {numPotions.Value} potions");
            }
            else if (text == "p" || text == "potion")
            {
                if (numPotions.Value == 0)
                {
                    Console.WriteLine("You're out of potions.");
                }
                else
                {
                    scene.IsActive = false;
                    Console.WriteLine("Press the number for the party member to use it on.");
                    var alive = this.party.Where(p => p.CurrentHealth > 0);
                    foreach (var i in Enumerable.Range(0, alive.Count()))
                    {
                        var member = alive.ElementAt(i);
                        Console.WriteLine($"{i}: {member.Specialization}, {member.CurrentHealth} out of {member.TotalHealth} health");
                    }
                    
                    var partyNum = -1;
                    while (!int.TryParse(Console.ReadLine(), out partyNum))
                    {
                        Console.WriteLine($"Try again - enter a number from 0 to {alive.Count() - 1}");
                    }

                    this.numPotions.Value -= 1;

                    var target = alive.ElementAt(partyNum);
                    var healed = (int)Math.Ceiling(Constants.HEAL_POTION_PERCENT * target.TotalHealth);
                    target.CurrentHealth = Math.Min(target.TotalHealth, target.CurrentHealth + healed);
                    Console.WriteLine($"Healed {healed} HP, {target.Specialization} now has {target.CurrentHealth} out of {target.TotalHealth} health.");
                    scene.IsActive = true;
                }
            }
        }

        public void Dispose()
        {
            this.isRunning = false;
            this.replThread.Join();
        }

        internal void StateParties(List<Unit> party, List<Unit> monsters)
        {
            this.party = party;
            this.monsters = monsters;

            StringBuilder partyText = new StringBuilder();
            
            partyText.Append("Your party includes: ");
            foreach (var member in party)
            {
                partyText.Append($"A level {member.Level} {member.Specialization}, ");
            }
            
            partyText.Append("They are facing: 5 slimes.");
            Console.WriteLine(partyText.ToString());
        }
    }
}