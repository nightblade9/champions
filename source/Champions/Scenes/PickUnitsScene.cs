using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DeenGames.Champions.Models;
using Puffin.Core;
using Puffin.Core.Ecs;
using Puffin.Core.Ecs.Components;

namespace DeenGames.Champions.Scenes
{
    class PickUnitsScene : Scene
    {
        private const string DEFAULT_LABEL_TEXT = "Mouse over a unit to see it's stats";
        private const int NUM_CHOICES = 20;
        private const int IMAGE_SIZE = 32;

        public PickUnitsScene()
        {
            var units = this.GenerateUnits();

            var label = new Entity().Label(DEFAULT_LABEL_TEXT).Move(32, ChampionsGame.GAME_HEIGHT - 32);
            this.Add(label);

            for (var i = 0; i < units.Count; i++)
            {
                var unit = units[i];
                var relativeX = (int)(1.5 * ((i % 5) * IMAGE_SIZE));
                var relativeY = (int)(1.5 * (i / 5) * IMAGE_SIZE);

                Console.WriteLine($"Hi there: {unit.Specialization} / {(int)unit.Specialization}");

                this.Add(new Entity()
                    .Move(300 + relativeX, 100 + relativeY)
                    .Spritesheet(Path.Combine("Content", "Images", "Specializations.png"), IMAGE_SIZE, IMAGE_SIZE, (int)unit.Specialization)
                    .Overlap(IMAGE_SIZE, IMAGE_SIZE, 0, 0,
                        () => label.Get<TextLabelComponent>().Text = $"Level {unit.Level} {unit.Specialization.ToString()}",
                        () => label.Get<TextLabelComponent>().Text = DEFAULT_LABEL_TEXT
                    ));
            }
        }

        private IList<Unit> GenerateUnits()
        {
            var random = new Random();
            var toReturn = new List<Unit>();
            var specializations = Enum.GetValues(typeof(Specialization));

            while (toReturn.Count < NUM_CHOICES)
            {
                var specialization = (Specialization)specializations.GetValue(random.Next(specializations.Length));
                var level = random.Next(1, 4);
                toReturn.Add(new Unit(specialization, level));
            }

            return toReturn;
        }
    }
}