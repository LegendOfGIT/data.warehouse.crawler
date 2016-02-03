using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

using Data.Web;

using CsQuery;
using Newtonsoft.Json;

namespace Data.Warehouse.Crawler
{
    public class WebcrawlingUtility {
        private DataWarehouseProvider DataWarehouseProvider = default(DataWarehouseProvider);
        public Dictionary<string, IEnumerable<string>> ContextDictionary { get; set; }
        public List<WebcrawlerCommand> ContextCommandset { get; set; }
        public Encoding PageEncoding { get; set; }

        public WebcrawlingUtility(DataWarehouseProvider storageprovider = null)
        {
            this.ContextDictionary = new Dictionary<string, IEnumerable<string>> { };
            this.DataWarehouseProvider = storageprovider;
        }

        public void Crawling(IEnumerable<WebcrawlerCommand> commandset, IEnumerable<string> contents = null) {
            if (commandset != null)
            {
                contents = contents ?? new[] { string.Empty };
                foreach (var content in contents)
                {
                    var storetarget = string.Empty;

                    foreach (var command in commandset)
                    {
                        var doRepeat = true;
                        while (doRepeat)
                        {
                            var subcommandContents = default(List<string>);

                            //  Anwendung eines Kommandos
                            var querytext = command.Command;
                            var queryattribute = command.AttributID;

                            var wasCommandApplied = default(bool);
                            //  Ausführen des Querys
                            if (!string.IsNullOrEmpty(querytext) || !string.IsNullOrEmpty(queryattribute))
                            {
                                var querytarget = command.Target;
                                var commandtokens = default(string[]);
                                commandtokens = (querytarget ?? string.Empty).Split('.');
                                var isTargetInformationItem = IsInformationItem(querytarget);

                                //  Ermittlung des Queryergebnis
                                var querycontent = FindContent(
                                    content,
                                    command
                                );

                                //  Bei einem Informationsobjekt als Zieltyp ...
                                commandtokens = (querytarget ?? string.Empty).Split('.');
                                if (isTargetInformationItem)
                                {
                                    //  ... wird bei erster Verwendung/einer Wiederholung der Index des Zielobjektes hochgezählt
                                    var targetbase = commandtokens.First();
                                    var lastObjectCommand = ContextCommandset.Last(c => c.Target.StartsWith(targetbase + "."));

                                    if(command == lastObjectCommand)
                                    {
                                        storetarget = $"{targetbase}.";
                                    }
                                }

                                var isCrawlingCommand = WebcrawlingUtilityConstants.Commands.Any(commandid => querytext.ToLower().Contains(commandid.ToLower()));
                                //  Die Abfrage ergab mindestens einen Treffer => Inhaltsverarbeitung
                                if (!isCrawlingCommand)
                                {
                                    foreach (var c in querycontent)
                                    {
                                        subcommandContents = subcommandContents ?? new List<string>();
                                        subcommandContents.Add(c);
                                    }

                                    //  Inhalt im Ziel sichern
                                    if (!string.IsNullOrEmpty(querytarget) && querycontent.Any())
                                    {
                                        AddInformationToken(
                                            content,
                                            command, 
                                            querytarget, 
                                            isTargetInformationItem, 
                                            querycontent
                                        );
                                    }
                                }
                                //  Kein Treffer => Sonstiger Befehl
                                else
                                {
                                    if (!string.IsNullOrEmpty(querytarget))
                                    {
                                        ContextDictionary[querytarget] = new[] { string.Empty };
                                    }

                                    commandtokens = querytext.Split(':');
                                    var commandobject = commandtokens.FirstOrDefault() ?? string.Empty;
                                    var commandexpression = commandtokens.Count() > 1 ? commandtokens.Skip(1).FirstOrDefault() ?? string.Empty : string.Empty;

                                    //  Befehle
                                    //  Browse()
                                    switch (commandexpression.ToLower())
                                    {
                                        case WebcrawlingUtilityConstants.CommandBrowse:
                                        {
                                            var uris = ContextDictionary.ContainsKey(commandobject) ? ContextDictionary[commandobject] ?? default(string[]) : default(string[]);
                                            if (uris != null)
                                            {
                                                foreach (var uri in uris)
                                                {
                                                    var browseuri = uri.Trim();
                                                    if (!string.IsNullOrEmpty(browseuri))
                                                    {
                                                        subcommandContents = subcommandContents ?? new List<string>();
                                                        subcommandContents.Add(WebUtility.GetWebsiteContent(browseuri));
                                                        wasCommandApplied = true;
                                                    }
                                                }
                                            }

                                            //  Bei einer regulären Variable wird der Inhalt der Variablen nach dem Browsen zurückgesetzt
                                            if(!IsInformationItem(commandobject))
                                            {
                                                ContextDictionary[commandobject] = Enumerable.Empty<string>();
                                            }

                                            break;
                                        }
                                    }
                                }
                            }

                            //  Ausführen der Subkommandos
                            if (command.Subcommands != null)
                            {
                                Crawling(command.Subcommands, subcommandContents);
                            }

                            doRepeat =
                                command.IsLoop && wasCommandApplied
                            ;
                        }
                    }

                    //  Wenn das storetarget gesetzt wurde (letzter Zielverweis auf Informationsobjekt), wird das Informationsobjekt über einen Provider gesichert.
                    if (!string.IsNullOrEmpty(storetarget) && this.DataWarehouseProvider != null)
                    {
                        var storedictionary = new Dictionary<string, IEnumerable<object>>();
                        foreach (var entry in this.ContextDictionary.Where(e => e.Key.StartsWith(storetarget)))
                        {
                            storedictionary[entry.Key] = entry.Value;
                        }

                        if(storedictionary != null && storedictionary.Any())
                        { 
                            this.DataWarehouseProvider.StoreInformation(storedictionary);
                        }

                        storedictionary.ToList().ForEach(storeitem => { ContextDictionary.Remove(storeitem.Key); });
                    }
                }
            }
        }

        /// <summary>
        /// Fügt dem Dictionary eine gesammelte Information hinzu
        /// </summary>
        /// <param name="currentContent">Webseiten Teilinhalt in dem gerade gelesen wird.</param>
        /// <param name="command">Kommando für das Lesen von Websiteabschnitten</param>
        /// <param name="querytarget">Ziel in dem der ermittelte Informationsteil abgelegt werden soll</param>
        /// <param name="isTargetInformationItem">Handelt es sich bei dem Ziel für den Informationsteil um ein Informationsitem</param>
        /// <param name="querycontent">Neu ausgelesener Teilinhalt der Website</param>
        private void AddInformationToken(
            string currentContent,
            WebcrawlerCommand command, 
            string querytarget, 
            bool isTargetInformationItem, 
            IEnumerable<string> querycontent
        )
        {
            var isFirstStoreCommand = ContextCommandset.First(storecommand => storecommand.Target == querytarget) == command;

            //  Die Information wird hinzugefügt, wenn ...
            if(
                //  kein Informationsitem ergänzt werden soll, oder ...
                !isTargetInformationItem ||
                //  der aktuell gefundene Inhalt nicht leer ist. (Sonst werden statisch hinzugefügte Informationen in einem Informationsitem abgelegt, obwohl es gar kein Informationsitem gibt.)
                !string.IsNullOrEmpty(currentContent)
            )
            { 
                //  Erstes Speicherkommando >> Neuanlage einer Informationswiederholung
                if (isFirstStoreCommand || !isTargetInformationItem)
                {
                    var storecontent = ContextDictionary.ContainsKey(querytarget) ? new List<string>(ContextDictionary[querytarget]) : new List<string>();
                    storecontent.AddRange(querycontent);
                    ContextDictionary[querytarget] = storecontent;
                }
                //  Ergänzen der letzten Speicherinformation
                else
                {
                    var storeitems = ContextDictionary.ContainsKey(querytarget) ? ContextDictionary[querytarget] : default(IEnumerable<string>);
                    var lastStoreitem = storeitems != null && storeitems.Any() ? storeitems.Last() : default(string);
                    if (lastStoreitem != null)
                    {
                        lastStoreitem += string.Format(" {0}", string.Join(" ", querycontent));
                        var storecontent = ContextDictionary[querytarget].ToList();
                        storecontent[storecontent.Count - 1] = lastStoreitem;
                        ContextDictionary[querytarget] = storecontent;
                    }
                }
            }
        }
        private IEnumerable<string> FindContent(string context, WebcrawlerCommand command)
        {
            var content = new List<string>();

            var originalQuerytext = command?.Command;

            //  Inhalt über JSON suchen
            if (!content.Any())
            {
                if ((context ?? string.Empty).StartsWith("{"))
                {
                    dynamic json = JsonConvert.DeserializeObject(context);
                    var jsonvalue = json?.Property(originalQuerytext);
                    if(!string.IsNullOrEmpty(jsonvalue?.Value.ToString()))
                    {
                        content = new List<string> { jsonvalue.Value.ToString() };
                    }
                }
            }

            //  Inhalt über CSS-Query suchen
            if (!content.Any())
            {
                var queryattribute = command?.AttributID;
                var quertarget = command?.Target;

                if (!string.IsNullOrEmpty(context) || !string.IsNullOrEmpty(queryattribute))
                {
                    var commandtokens = default(string[]);
                    commandtokens = (quertarget ?? string.Empty).Split('.');
                    var isTargetInformationItem = commandtokens?.Length > 1;
                    var dom = new CQ(context);
                    var query = default(CQ);
                    try
                    {
                        query = string.IsNullOrEmpty(originalQuerytext) ? dom : dom[originalQuerytext];
                    }
                    catch (Exception) { }

                    if (query != null && query.Any())
                    {
                        try { 
                            content = query.Select(
                                item =>
                                    string.IsNullOrEmpty(queryattribute) ?
                                        isTargetInformationItem ?
                                        item.InnerHTML :
                                        item.OuterHTML :
                                    item.Attributes[queryattribute] ?? string.Empty
                            ).ToList();
                        }
                        catch (Exception) { }
                    }
                }
            }

            //  Inhalt über Regularexpression suchen
            if (!content.Any())
            {
                try
                { 
                    var expression = (@"" + originalQuerytext ?? string.Empty);
                    var matches = 
                        Regex.Matches(context, expression).
                        OfType<Match>().
                        Where(match => match.Success && !string.IsNullOrEmpty(match.Value)).
                        Select(match => match.Value)
                    ;                
                    if(matches != null && matches.Any())
                    {
                        content = matches.ToList();
                    }
                }
                catch (Exception) { }
            }

            //  Query in das Contextdictionary leiten
            if (!content.Any())
            {
                var tokens = originalQuerytext.Split(':');
                var querytext = tokens?.FirstOrDefault() ?? string.Empty;
                content = ContextDictionary.ContainsKey(querytext) ? ContextDictionary[querytext].ToList() : content;
            }

            //  Inhalt mit "" umschlossen >> Statischer Inhalt
            if (!content.Any())
            {
                var quotation = "\"";
                if (originalQuerytext.StartsWith(quotation) && originalQuerytext.EndsWith(quotation))
                {
                    content = originalQuerytext.Replace(quotation, string.Empty).Split(',').Select(token => token.Trim()).ToList();
                }
            }

            //  Soll für den aktuellen Inhalt eine Anpassung stattfinden?
            var querytarget = (command?.Target ?? string.Empty).ToLower();
            if(
                content != null && content.Any() && 
                !string.IsNullOrEmpty(querytarget) && 
                WebcrawlingUtilityConstants.BasicProperties.Any(prop => querytarget.EndsWith($"{prop}"))                
            )
            {
                for(int i = 0; i<content.Count();i++)
                {
                    var token = content[i];
                    
                    //  Bei URIs ggf. die Source-Uri ergänzen.
                    if (querytarget.EndsWith($"{WebcrawlingUtilityConstants.Uri}"))
                    {
                        if (!new[] { "http:", "https:" }.Any(prefix => token.StartsWith(prefix)))
                        {
                            var baseuri = ContextDictionary.ContainsKey(WebcrawlingUtilityConstants.BaseUri) ? ContextDictionary[WebcrawlingUtilityConstants.BaseUri].FirstOrDefault() ?? string.Empty : string.Empty;
                            var separator = !baseuri.EndsWith("/") && !token.StartsWith("/") ? "/" : string.Empty;
                            token = $"{baseuri}{separator}{token}";
                        }
                    }

                    //  Bei IDs und Corellations ggf. Site BasisUri als Prefix hinzufügen
                    if (new[] { WebcrawlingUtilityConstants.Corellation, WebcrawlingUtilityConstants.Id }.Any(prop => querytarget.EndsWith($".{prop}")) )
                    {
                        var sitename = this.ContextDictionary.ContainsKey(WebcrawlingUtilityConstants.BaseUri) ? this.ContextDictionary[WebcrawlingUtilityConstants.BaseUri].First() : string.Empty;
                        sitename = Regex.Split(sitename, @"://")?.Skip(1).FirstOrDefault() ?? string.Empty;
                        token = token.StartsWith(sitename) ? token : $"{sitename}.{token}";
                    }


                    content[i] = token;
                }
            }

            return content;
        }
        private bool IsInformationItem(string item)
        {
            var tokens = (item ?? string.Empty).Split('.');
            return
                !WebcrawlingUtilityConstants.BasicVariables.Contains(item) &&
                tokens?.Length > 1
            ;
        }
    }
}
