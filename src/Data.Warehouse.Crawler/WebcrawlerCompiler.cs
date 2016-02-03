using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Data.Warehouse.Crawler
{
    public class WebcrawlerCompiler
    {
        public List<WebcrawlerCommand> ContextCommandset { get; set; }

        public IEnumerable<WebcrawlerCommand> ParseCommandset(IEnumerable<string> lines, int index = default(int))
        {
            var commands = default(List<WebcrawlerCommand>);

            var line = lines.ToArray()[index];
            var level = line.GetLevel();
            var commandlines = new List<KeyValuePair<int, string>>();
            var followinglines = lines.Skip(index);
            foreach(var followingline in followinglines.Where(l => !string.IsNullOrEmpty(l)))
            {
                if (followingline.GetLevel() < level)
                {
                    break;
                }

                if (followingline.GetLevel() == level)
                {
                    commandlines.Add(new KeyValuePair<int, string>(followinglines.ToList().IndexOf(followingline) + index, followingline));
                }
            }

            foreach (var commandline in commandlines)
            {
                var commandtext = commandline.Value;
                var tokens = default(IEnumerable<string>);

                //  Enthält das Kommando ein !, wird dieses Kommando solange wiederholt wie es mindestens ein Ergebnis zurückgibt.
                var isLoop = commandtext.Contains("!");
                commandtext = commandtext.Replace("!", string.Empty);
                //  Ermittlung des Kommandoziels
                tokens = Regex.Split(commandtext, ">>");
                var target =
                    (tokens?.Count() > 1 ? (tokens.Skip(1).FirstOrDefault() ?? string.Empty) : string.Empty).Trim()
                ;
                commandtext = (tokens?.FirstOrDefault() ?? string.Empty).Trim();
                //  Ermittlung eines Attributes
                tokens = commandtext.Split('@');
                var attribute = tokens?.Count() > 1 ? (tokens.Skip(1).FirstOrDefault() ?? string.Empty).Trim() : string.Empty;
                commandtext = string.IsNullOrEmpty(attribute) ? commandtext : tokens.FirstOrDefault() ?? commandtext;

                commands = commands ?? new List<WebcrawlerCommand>();
                var command = new WebcrawlerCommand
                {
                    AttributID = attribute,
                    IsLoop = isLoop,
                    Command = commandtext,
                    Target = target
                };

                this.ContextCommandset = this.ContextCommandset ?? new List<WebcrawlerCommand>();
                var subcommands = default(List<WebcrawlerCommand>);
                var followingLine = new KeyValuePair<int, string>(commandline.Key + 1, lines.Skip(commandline.Key + 1).FirstOrDefault() ?? string.Empty);
                if (!string.IsNullOrEmpty(followingLine.Value) && followingLine.Value.GetLevel() == level + 1)
                {
                    subcommands = subcommands ?? new List<WebcrawlerCommand>();
                    var set = ParseCommandset(lines, followingLine.Key);
                    subcommands.AddRange(set);
                }

                command.Subcommands = subcommands;
                commands.Add(command);
                this.ContextCommandset.Add(command);
            }

            return commands;
        }
    }
}
