/*
 * MIT-Lizenz
 * 
 * Copyright (c) 2019 Sergej Voltchek
 * 
 * Hiermit wird unentgeltlich jeder Person, die eine Kopie der Software
 * und der zugehörigen Dokumentationen (die "Software") erhält, die Erlaubnis erteilt,
 * sie uneingeschränkt zu nutzen, inklusive und ohne Ausnahme mit dem Recht, 
 * sie zu verwenden, zu kopieren, zu verändern, zusammenzufügen, zu veröffentlichen,
 * zu verbreiten, zu unterlizenzieren und/oder zu verkaufen, und Personen,
 * denen diese Software überlassen wird, diese Rechte zu verschaffen, unter den folgenden Bedingungen:
 * 
 * Der obige Urheberrechtsvermerk und dieser Erlaubnisvermerk sind in allen Kopien oder Teilkopien der Software beizulegen.
 * 
 * DIE SOFTWARE WIRD OHNE JEDE AUSDRÜCKLICHE ODER IMPLIZIERTE GARANTIE BEREITGESTELLT,
 * EINSCHLIEẞLICH DER GARANTIE ZUR BENUTZUNG FÜR DEN VORGESEHENEN ODER EINEM BESTIMMTEN 
 * ZWECK SOWIE JEGLICHER RECHTSVERLETZUNG, JEDOCH NICHT DARAUF BESCHRÄNKT. 
 * IN KEINEM FALL SIND DIE AUTOREN ODER COPYRIGHTINHABER FÜR JEGLICHEN SCHADEN
 * ODER SONSTIGE ANSPRÜCHE HAFTBAR ZU MACHEN, OB INFOLGE DER ERFÜLLUNG EINES VERTRAGES,
 * EINES DELIKTES ODER ANDERS IM ZUSAMMENHANG MIT DER SOFTWARE ODER SONSTIGER VERWENDUNG DER SOFTWARE ENTSTANDEN.
 * 
 * 
 * MIT License
 *
 * Copyright (c) 2019 Sergej Voltchek
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:

 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.

 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */

using System;
using System.IO;
using Newtonsoft.Json;
using Easy.Business.Common;
using Easy.Business.Core;
using System.Data.SqlClient;
using Easy.Business;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Data;
using log4net;
using ezhelper = Easy.Business.Samples;

namespace EbisExporter
{
    class Program
    {
        /// <summary>
        /// startpfad der applikation für konfigurationsdatei notwendig
        /// </summary>
        private static string applicationpath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);

        /// <summary>
        /// erzeugt den logger
        /// </summary>
        private static readonly ILog log = LogManager.GetLogger("EbisExporter");

        /// <summary>
        /// name der konfigurationsdatei
        /// </summary>
        private const string propertyfilename = "ebisexporter.properties";

        /// <summary>
        /// logging konfiguration
        /// </summary>
        private const string logconfigfilename = "ebisexporterlog.xml";

        /// <summary>
        /// objekt mit geparsten parametern zum späteren verarbeiten 
        /// </summary>
        private static EbisExporterProperties properties = null;

        /// <summary>
        /// verbindung zur datenbank
        /// </summary>
        private static SqlConnection sqlconnection = null;

        /// <summary>
        /// speichert den flag beim abbruch durch den benutzer
        /// </summary>
        private static bool cancelation = false;

        /// <summary>
        /// speichert den flag beim beenden vom export
        /// </summary>
        private static bool finished = false;

        /// <summary>
        /// speichert die aktuelle ausgabe für die console
        /// </summary>
        private static string status = string.Empty;

        /// <summary>
        /// sitzung in ebis
        /// </summary>
        private static Session session = null;

        /// <summary>
        /// repository im ebis
        /// </summary>
        private static Repository repository = null;

        /// <summary>
        /// speichert den namen der referenztabelle
        /// </summary>
        private static string referencetablename = string.Empty;

        /// <summary>
        /// speichert den namen der indextabelle
        /// </summary>
        private static string indextablename = string.Empty;

        /// <summary>
        /// speichert den namen der blobtabelle
        /// </summary>
        private static string blobstablename = string.Empty;

        /// <summary>
        /// speichert den namen der notiztabelle
        /// </summary>
        private static string notestablename = string.Empty;

        /// <summary>
        /// zeigt den aktuellen status beim referenzen export an
        /// </summary>
        private static Status referenztaskstatus = Status.unbekannt;

        /// <summary>
        /// zeigt den status text beim referenezen export an
        /// </summary>
        private static string referenztaskstatustext = string.Empty;

        /// <summary>
        /// zeigt den status text für den export an
        /// </summary>
        private static string exporttaskstatustext = string.Empty;

        /// <summary>
        /// zeigt den aktuellen status beim archivexport an
        /// </summary>
        private static Status exporttaskstatus = Status.unbekannt;

        /// <summary>
        /// speichert die anzahl der maximalen referenz rückgaben im select
        /// </summary>
        private const byte maxreferences = 2;

        /// <summary>
        /// maximale anzahl an fehlern beim export
        /// sonst bleiben wir in einer endlosschleife liegen..
        /// </summary>
        private const byte maxerrors = 20;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="args"></param>
        static void Main(string[] args)
        {
            log.Debug(string.Format("ebisexporter gestartet"));

            // property datei suchen
            string propertyfile = Path.Combine(applicationpath, propertyfilename);

            log.Debug(string.Format("konfigurationsdatei: {0}", propertyfile));

            try
            {
                // logging mit log4net
                GlobalContext.Properties["LogPath"] = applicationpath;
                log4net.Config.XmlConfigurator.Configure(new System.IO.FileInfo(Path.Combine(applicationpath, logconfigfilename)));

                log.Debug(string.Format("log pfad: {0}", applicationpath));

                // prüfen ob das propertyfile existiert oder erstellt es
                CheckIfPropertyFile(propertyfile);

                log.Debug(string.Format("Konfigurationfile gefunden"));

                // einlesen und prüfen der konfiguration
                properties = ReadAndParsePropertyFile(propertyfile);

                log.Debug(string.Format("Konfigurationfile geparst"));

                // öffnen der sql verbindung
                sqlconnection = CreateSqlConnection(properties.ConnectionString);

                log.Debug(string.Format("SQL Verbindung erzeugt"));

                // öffnen der verbindung zum ebis server
                ClientService service = new DefaultClientService(properties.ClientUrl); // client erzeugen
                Instance instance = service.GetInstance(properties.Instance); // instanz suchen, sollte vorher angelegt sein ;-)
                session = instance.Login(properties.ClientUser, properties.ClientPassword, new Dictionary<string, string>()); // einloggen
                repository = session.GetRepository(properties.Repository);

                log.Debug(string.Format("Verbindung zum EBIS Server erzeugt"));

                // schema suchen und dt anhand dessen erstellen
                foreach (DocumentSchema schema in repository.DocumentSchemes)
                {
                    if (!schema.Name.Equals(properties.Schema))
                        continue;
                    
                    // erzeugen der Tabellen
                    CreateExportTablesFromSchema(sqlconnection, schema);

                    log.Debug(string.Format("tabellen erzeugt"));
                }

                /*
                 * separate threads zum schreiben der referenzen und
                 * gleichzeitigen export wenn die archive riesen
                 * groß sind sollte man es parallel machen, weil das 
                 * erstellen der refliste mehrere stunden dauern kann
                 */
                Thread referenceThread = new Thread(WriteReferences);
                referenceThread.Start();

                log.Debug(string.Format("Referenz-Thread gestartet"));

                Thread exportThread = new Thread(Export);
                exportThread.Start();

                log.Debug(string.Format("Export-Thread gestartet"));


                Console.Clear();
                Console.CursorVisible = false;
                Console.WriteLine("Export läuft...");
                Console.WriteLine("[X] drücken zum stoppen und abbrechen");
                Console.WriteLine();

                while (true)
                {
                    // X zum anhalten
                    if(Console.KeyAvailable)
                    {
                        ConsoleKeyInfo info = Console.ReadKey();

                        if (info.Key == ConsoleKey.X)
                            cancelation = true;
                    }

                    //
                    Console.CursorTop = 3;
                    Console.CursorLeft = 0;
                    Console.WriteLine("Derzeitiger Task: {0}", status);
                    Console.WriteLine("TASK - Referenzen erzeugen");
                    Console.WriteLine("Status: {0}", referenztaskstatus.ToString());
                    Console.WriteLine("Info:");
                    Console.WriteLine(referenztaskstatustext);
                    Console.WriteLine("TASK - Dokumenten Export:");
                    Console.WriteLine("Status: {0}", exporttaskstatus.ToString());
                    Console.WriteLine("Info:");
                    Console.WriteLine(exporttaskstatustext);

                    // bei abbruch oder wenn alles erfolgreich exportiert wurde
                    if (cancelation || finished)
                    {
                        if (cancelation)
                        {
                            Console.Clear();
                            Console.WriteLine("Vorgang durch Benutzer abgebrochen!");
                        }

                        if (finished)
                        {
                            Console.Clear();
                            Console.WriteLine("Abgeschlossen!");
                            Console.WriteLine("Referenz-Status: {0}", referenztaskstatus.ToString());
                            Console.WriteLine("Export-Status: {0}", exporttaskstatus.ToString());
                        }

                        //
                        break;
                    }
                        
                }

                // verbindung schließen
                if (session != null)
                    session.Logout();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

            Console.WriteLine("Beliebige Taste drücken zum beenden.");
            Console.ReadKey();
        }

        /// <summary>
        /// sucht nach noch nicht exportierten referenzenz und meldet diese zurück,
        /// 
        /// </summary>
        /// <returns></returns>
        static string[] NotExportedReferences()
        {
            List<string> queryitems = new List<string> { };

            string query = "SELECT TOP " + maxreferences + " reference FROM " + referencetablename + " WHERE reference NOT IN ";
            query += "(SELECT mappenid FROM " + indextablename + ");";

            using (SqlConnection connection = new SqlConnection(properties.ConnectionString))
            {
                // verbindung herstellen
                connection.Open();

                using (SqlCommand sqlcommand = new SqlCommand(query, connection))
                {
                    using (SqlDataReader sqlreader = sqlcommand.ExecuteReader())
                    {
                        if (sqlreader.HasRows)
                        {
                            while (sqlreader.Read())
                                queryitems.Add(sqlreader["reference"].ToString());
                        }
                    }
                }
            }
            
            //
            return queryitems.ToArray();
        }

        /// <summary>
        /// exportiert die vorgänge anhand gefundener referenzen aus der refererenzen tabelle
        /// </summary>
        static void Export()
        {
            // info enum setzen
            exporttaskstatus = Status.gestartet;

            log.Debug("export gestartet");


            int errorcounter = 0;
            string query = string.Empty;
            string insert = string.Empty;
            string values = string.Empty;
            string[] referenzedRows;

            using (SqlConnection connection = new SqlConnection(properties.ConnectionString))
            {
                // verbindung herstellen
                connection.Open();

                log.Debug("datenbank verbindung geöffnet");

                // go baby go...
                while (true)
                {
                    log.Debug("suche referenzen gestartet");

                    referenzedRows = NotExportedReferences();

                    if (referenzedRows != null && referenzedRows.Length > 0)
                    {
                        log.Debug(string.Format("{0} referenzen gefunden", referenzedRows.Length));

                        foreach (string row in referenzedRows)
                        {
                            /*
                             * prüfe ob die referenz existiert
                             * wenn nicht hinzufügen
                             * zum gewährleisten falls ein export abgebrochen 
                             * und neugestartet wird
                             */
                            query = "SELECT COUNT(*) FROM \"" + indextablename + "\"";
                            query += "WHERE mappenid = '" + row + "';";
                            using (SqlCommand command = new SqlCommand(query, connection)) // kommando für select und insert
                            {
                                // weiter wenn gefunden
                                if (((int)command.ExecuteScalar()) >= 1)
                                    continue;
                            }

                            //
                            Document eexdocument = session.GetDocument(row);

                            log.Debug(string.Format("exportiere dokument: {0}", eexdocument.Reference));

                            FieldContent[] allFields = eexdocument.Items.OfType<FieldContent>().ToArray();
                            BlobContent[] allBlobs = eexdocument.Items.OfType<BlobContent>().ToArray();
                            Easy.Business.Note[] allNotes = eexdocument.GetNotes();

                            string docid = eexdocument.ID;
                            string docversionid = eexdocument.Version;
                            string refid = docid + docversionid; // eindeutiger key

                            if (allFields != null)
                            {
                                // 
                                insert = "INSERT INTO " + indextablename + "([ROOTID],[VERSION],[refid],[mappenid],[importmappenid],[imported]";
                                values = " VALUES ('" + docid + "','" + docversionid + "','" + refid + "','" + eexdocument.Reference + "','','0'";

                                foreach (FieldContent field in allFields)
                                {
                                    if (!field.SchemaAttribute.SystemField)
                                    {
                                        insert += (",[" + field.Name + "]");
                                        values += (",'" +  (((field.Value != null) ? field.Value.ToString().Replace("'", "''") : "") + "'"));
                                    }
                                }

                                insert += ")";
                                values += ");";

                                query = insert + values;
                                log.Debug(query);

                                SqlTransaction indextransaction;
                                using (SqlCommand indexcommand = new SqlCommand(query, connection)) // kommando für select und insert
                                {
                                    /*
                                     * transaktion starten
                                     * rollback beo problemen mit blobs oder 
                                     */
                                    indextransaction = connection.BeginTransaction(string.Format("{0}", docid));
                                    indextransaction.Save(string.Format("{0}", docid));
                                    indexcommand.Transaction = indextransaction;
                                    indexcommand.StatementCompleted += Indexcommand_StatementCompleted;



                                    try
                                    {
                                        // importieren...
                                        if (indexcommand.ExecuteNonQuery() >= 1)
                                        {
                                            // blobs exportieren und in db wegschreiben
                                            if (allBlobs != null && allBlobs.Length > 0)
                                            {
                                                string blobpath = Path.Combine(properties.ExportPath, docid, docversionid);

                                                if (!Directory.Exists(blobpath))
                                                    Directory.CreateDirectory(blobpath);

                                                foreach (BlobContent blob in allBlobs)
                                                {
                                                    string blobid = blob.ID;
                                                    string originalfilename = blob.FileName;
                                                    string blobextension = new System.IO.FileInfo(originalfilename).Extension;

                                                    // eindeutigen namen erzeugen, originalname geht in datenbank
                                                    string uniqueexportpath = Path.Combine(blobpath, string.Format(@"{0}{1}", Guid.NewGuid(), blobextension));

                                                    try
                                                    {
                                                        ezhelper.BlobHelper.StoreBlobToFile(blob, uniqueexportpath);

                                                        // prüfen ob die datei weggeschrieben wurde
                                                        if (File.Exists(uniqueexportpath))
                                                        {
                                                            //
                                                            insert = "INSERT INTO " + blobstablename + "([refid],[pfad],[dateiname],[orgdateiname])";
                                                            values = " VALUES ('" + refid + "','" + Path.GetDirectoryName(uniqueexportpath) + @"\', N'" + Path.GetFileName(uniqueexportpath) + "', '" + originalfilename + "')";

                                                            //
                                                            query = insert + values;
                                                            log.Debug(query);

                                                            using (SqlCommand blobcommand = new SqlCommand(query, connection)) // kommando für select und insert
                                                            {
                                                                blobcommand.Transaction = indextransaction; // transaktion vom index übernehmen, es soll alles entfernt werden, wenn ein fehler passiert

                                                                try
                                                                {
                                                                    if (blobcommand.ExecuteNonQuery() < 1)
                                                                        throw new Exception("Blob nicht eingefügt");

                                                                }
                                                                catch (Exception blobexception)
                                                                {
                                                                    throw new Exception(string.Format("Blobimport fuer Mappe: {0} ist fehlgeschlagen: {1}", docid, blobexception.Message));
                                                                }
                                                            }
                                                        }
                                                        else
                                                        {
                                                            // falls die datei, warum auch immer nicht gefunden wird...
                                                            throw new FileNotFoundException(string.Format("Die exportierte Datei {0} konnte nicht gefunden werden", uniqueexportpath));
                                                        }
                                                    }
                                                    catch (Exception storeblobexception)
                                                    {
                                                        // dateien löschen falls welche schon exportiert wurden
                                                        try
                                                        {
                                                            Directory.Delete(blobpath, true);
                                                        }
                                                        catch (Exception storedblobsdelete)
                                                        {
                                                            log.Fatal("Löschen der vorhandenen Blobs - Exception", storedblobsdelete);

                                                            throw new Exception(string.Format("Gespeicherten Dateien konnten nicht gelöscht werden. {0}", storedblobsdelete.Message));
                                                        }

                                                        log.Fatal("Speichern von Blobs - Exception", storeblobexception);


                                                        // durchreichen ;-)
                                                        throw storeblobexception;
                                                    }
                                                }
                                            }

                                            // notizen rausschreiben
                                            if (allNotes != null)
                                            {
                                                foreach (Easy.Business.Note note in allNotes)
                                                {
                                                    insert = "INSERT INTO " + blobstablename + "([refid],[username],[datum],[inhalt])";
                                                    values = " VALUES ('" + refid + "', '0','" + note.Owner + "', '" + note.Date + "', '" + note.Value + "')";

                                                    query = insert + values;
                                                    log.Debug(query);

                                                    using (SqlCommand notecommand = new SqlCommand(query, connection)) // kommando für select und insert
                                                    {
                                                        notecommand.Transaction = indextransaction;

                                                        try
                                                        {
                                                            if (notecommand.ExecuteNonQuery() < 1)
                                                                throw new Exception("Notiz nicht angelegt!"); // sollte eigentlich nie passueren :)
                                                        }
                                                        catch (Exception noteexception)
                                                        {
                                                            log.Fatal("Notizenexport Exception", noteexception);

                                                            throw new Exception(string.Format("Notizenimport fuer Mappe: {0} ist fehlgeschlagen: {1}", docid, noteexception.Message));
                                                        }
                                                    }
                                                }
                                            }
                                        }

                                        // commiten falls erfolgreich
                                        indextransaction.Commit();

                                        // 
                                        exporttaskstatustext = string.Format("Mappe {0} erfolgreich exportiert", docid);

                                        log.Debug(exporttaskstatustext);
                                    }
                                    catch (Exception îndexexception)
                                    {
                                        // 
                                        exporttaskstatustext = "Fehler beim Exportieren. Rollback wird ausgeführt.";

                                        log.Fatal("Fehler beim Exportieren. Rollback wird ausgeführt.", îndexexception);

                                        // 
                                        string indextransactionresultmessage = string.Empty;
                                        bool indextransactionresult = false;

                                        // bei fehlschlag rollback
                                        try
                                        {
                                            // rollback wird ausgeführt
                                            indextransaction.Rollback(string.Format("{0}", docid));

                                            //
                                            indextransactionresult = true;
                                            indextransactionresultmessage = "Rollback ausgeführt.";

                                        }
                                        catch (Exception indexrollbackfailed)
                                        {
                                            // error handling für rollback
                                            indextransactionresultmessage = indexrollbackfailed.Message;

                                            log.Fatal("Fehler beim Exportieren.", îndexexception);
                                        }

                                        log.Debug(string.Format("Fehler beim Exportieren. Rollback {0} - Message: {1}.", indextransactionresult, indextransactionresultmessage));

                                        // fehleranzahl erhöhen
                                        errorcounter++;
                                    }
                                    finally
                                    {
                                        if (indextransaction != null)
                                            indextransaction.Dispose();
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        log.Debug("keine referenzen gefunden");

                        if (referenztaskstatus == Status.abgeschlossen)
                        {
                            log.Debug("referenztaskstatus nicht abgeschlossen, suche weiter..");

                            finished = true;
                        }
                    }        
                    
                    //
                    if (cancelation)
                    {
                        log.Debug("export abgebrochen");
                        break;
                    }
                       

                    //
                    if (finished)
                    {
                        exporttaskstatustext = "Export abgeschlossen";
                        exporttaskstatus = Status.abgeschlossen;

                        log.Debug(exporttaskstatustext);


                        break;
                    }
                        
                    // fehler fall
                    if (errorcounter >= maxerrors)
                    {
                        exporttaskstatus = Status.fehler;
                        exporttaskstatustext = string.Format("Maximale Anzahl an Fehlern {0} überschritten.", maxerrors);

                        log.Debug(exporttaskstatustext);

                        break;
                    }
                }
            }
        }

        
        /// <summary>
        /// schreibt die gefundenen referenzen in die sql tabelle
        /// </summary>
        static void WriteReferences()
        {
            // info enum setzen
            referenztaskstatus = Status.gestartet;

            log.Debug("referenzen exporttask gestartet");

            while (true)
            {
                // suche starten
                Search search = session.CreateSearch();
                search.AddSource(repository);

                // Restrict to maximum 10 entries
                search.MaxHits = 1000000000;
                search.PageSize = 100;


                // suchen
                SearchResult result = search.Execute();

                //
                ResultRow[] rows;

                string query = string.Empty;
                string mappenid = string.Empty;
                string insert = string.Empty;
                string values = string.Empty;
                bool exists = false;

                using (SqlConnection connection = new SqlConnection(properties.ConnectionString))
                {
                    // verbindung herstellen
                    connection.Open();


                    while ((rows = result.NextPage()) != null)
                    {
                        foreach (ResultRow row in rows)
                        {
                            mappenid = row.Reference;
                            exists = false;

                            /*
                             * prüfe ob die referenz existiert
                             * wenn nicht hinzufügen
                             * zum gewährleisten falls ein export abgebrochen 
                             * und neugestartet wird
                             */
                            query = "SELECT COUNT(*) FROM \"" + referencetablename + "\"";
                            query += "WHERE reference = '" + mappenid + "';";
                            using (SqlCommand command = new SqlCommand(query, connection))
                            {
                                using (SqlDataReader reader = command.ExecuteReader())
                                {
                                    if (reader.HasRows)
                                    {
                                        while (reader.Read())
                                        {
                                            if (reader.GetInt32(0) >= 1)
                                                exists = true;
                                        }
                                    }
                                }
                            }

                            // falls existiert, weiter
                            if (exists)
                                continue;

                            insert = "INSERT INTO " + referencetablename + "(reference)";
                            values = " VALUES ('" + mappenid + "');";
                            query = insert + values;
                            using (SqlCommand command = new SqlCommand(query, connection))
                            {
                                command.ExecuteNonQuery();

                                //
                                referenztaskstatustext = string.Format("Referenz {0} erfolgreich geschrieben.", mappenid);
                                log.Debug(referenztaskstatustext);
                            }

                            if (cancelation)
                                break;
                        }
                    }

                    if (result.IsExecuted())
                        break;

                    // abbruch
                    if (cancelation)
                    {
                        referenztaskstatustext = string.Format("Referenzexport abgebrochen.");
                        referenztaskstatus = Status.abgebrochen;

                        log.Debug(referenztaskstatustext);

                        break;
                    }
                }          
            }

            // abgeschlossen
            referenztaskstatustext = string.Format("Referenzexport abgeschlossen.");
            referenztaskstatus = Status.abgeschlossen;

            log.Debug(referenztaskstatustext);
        }

        /// <summary>
        /// erzeugt tabellen auf der datenbank zum späteren befüllen der exportdaten
        /// hierbei werden vier tabellen erezeugt
        /// - referenzen
        /// - index
        /// - blobs
        /// - notizen
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="schema"></param>
        static void CreateExportTablesFromSchema(SqlConnection connection, DocumentSchema schema)
        {
            // prefix für tabellennamen festlegen
            string tableprefix = string.Format("export_{0}_{1}", properties.Repository.ToLower(), properties.Schema.ToLower());

            // namen für die exportabellen festellegen aus dem prefix
            referencetablename = (tableprefix + "_reference");
            indextablename = (tableprefix + "_index");
            blobstablename = (tableprefix + "_blobs");
            notestablename = (tableprefix + "_notes");

            //
            bool exists = false;

            // prüfen ob tabellen bereits existieren
            if(SqlTableExists(referencetablename) || SqlTableExists(indextablename) || 
                SqlTableExists(blobstablename) || SqlTableExists(notestablename))
            {
                //
                exists = true;

                Console.WriteLine("Die Tabellen für den angegebenen Export existieren bereits.");
                Console.WriteLine("Sollen die Tabellen und Dateien gelöscht werden [J]? oder versuchen den Export wiederaufzunehmen [N]?");
                Console.WriteLine("[J] = Löschen und neu exportieren; [N] = Export wiederaufnehmen; DEFAULT = [J]");

                while(true)
                {
                    // lesen der eingabe
                    string result = Console.ReadLine();

                    // löschen und neu exportieren
                    if (string.IsNullOrEmpty(result) || result.ToUpper().Equals("J"))
                    {
                        DropTables(connection, new string[] { referencetablename, indextablename, blobstablename, notestablename });
                        Directory.Delete(properties.ExportPath, true);
                        //
                        exists = false;

                        break;
                    }

                    // weiter versuchen zu schreiben
                    if (result == "N")
                        break;
                }
            }

            if(!exists)
            {
                // erzeugen der tabellen
                CreateReferenceTable(); // referenztabelle erzeugen
                CreateIndexTable(schema); // indextabelle erzeugen
                CreateBlobTable(); // blobtabelle erzeugen
                CreateNotesTable(); // notiztabelle erzeugen
            }
            
            //
            if (!SqlTableExists(referencetablename) || !SqlTableExists(indextablename) ||
                !SqlTableExists(blobstablename) || !SqlTableExists(notestablename))
                throw new ApplicationException("Tabellen wurden nicht erzeugt! Vorgang abgebrochen!");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="command"></param>
        static void ExecuteCreateTableCommand(string command)
        {
            using (SqlCommand sqlcommand = new SqlCommand(command, sqlconnection))
            {
                int result = sqlcommand.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="tablename"></param>
        static void CreateNotesTable()
        {
            string command = "CREATE TABLE \"" + notestablename + "\" ";
            command += "([uid] [int] IDENTITY(1,1) NOT NULL, ";
            command += "[refid] [varchar] (255) NULL, ";
            command += "[username] [varchar](255) NULL, ";
            command += "[datum]  [varchar] (255) NULL, ";
            command += "[inhalt] [varchar] (MAX) NULL ";
            command += "PRIMARY KEY CLUSTERED ([uid] ASC))";
            
            //
            ExecuteCreateTableCommand(command);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="tablename"></param>
        static void CreateBlobTable()
        {
            string command = "CREATE TABLE \"" + blobstablename + "\" ";
            command += "([uid] [int] IDENTITY(1,1) NOT NULL, ";
            command += "[refid] [varchar ](255) NULL, ";
            command += "[pfad] [varchar] (255) NULL, ";
            command += "[dateiname] [varchar] (255) NULL, ";
            command += "[orgdateiname] [varchar] (255) NULL ";
            command += "PRIMARY KEY CLUSTERED ([uid] ASC))";

            //
            ExecuteCreateTableCommand(command);
        }

        /// <summary>
        /// erzeugt die tabellen für den export der referenzen
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="tablename"></param>
        static void CreateReferenceTable()
        {
            string command = "CREATE TABLE \"" + referencetablename + "\" ";
            command += "([uid][int] IDENTITY(1, 1) NOT NULL, ";
            command += "[reference] [varchar](255) NULL ";
            command += "PRIMARY KEY CLUSTERED ([uid] ASC))";

            //
            ExecuteCreateTableCommand(command);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="tablename"></param>
        static void CreateIndexTable(DocumentSchema schema)
        {
            string command = "CREATE TABLE \"" + indextablename + "\" ";
            command += "([uid][int] IDENTITY(1, 1) NOT NULL, ";
            command += "[refid] [varchar](255) NULL, ";
            command += "[mappenid] [varchar](255) NULL, ";
            command += "[importmappenid] [varchar](255) NULL, ";
            command += "[imported] [bit] NOT NULL ";
            

            foreach (Easy.Business.SchemaAttribute attribute in schema.Attributes.Where(a => a.Type == Easy.Business.AttributeType.FIELD && !String.IsNullOrEmpty(a.Name) && ((a.IndexField == true && a.SystemField == false) || (a.IndexField == true && a.SystemField == true) || (a.IndexField == false && a.SystemField == false))).ToArray())
                command += string.Format(",[{0}] [varchar](MAX) NULL ", attribute.Name);

            command += "PRIMARY KEY CLUSTERED ([uid] ASC)) ";

            //
            ExecuteCreateTableCommand(command);
        }

        /// <summary>
        /// prüft ob die angegebene datenbanktabelle existiert
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="tablename"></param>
        /// <returns></returns>
        static bool SqlTableExists(string tablename)
        {
            SqlCommand command = new SqlCommand("SELECT COUNT(*) FROM [INFORMATION_SCHEMA].[TABLES] WHERE TABLE_NAME = '" + tablename + "';", sqlconnection);
            int result = (int)command.ExecuteScalar();
            
            //
            if (result >= 1)
                return true;
            return false;
        }

        /// <summary>
        /// löschen einer tabelle
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="tablename"></param>
        static void DropTable(string tablename)
        {
            if(SqlTableExists(tablename))
            {
                SqlCommand command = new SqlCommand("DROP TABLE \"" + tablename + "\";", sqlconnection);
                int result = command.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// mehrere tabellen löschen
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="tables"></param>
        static void DropTables(SqlConnection connection, string[] tables)
        {
            foreach (string table in tables)
                DropTable(table);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="connectionstring"></param>
        /// <returns></returns>
        static SqlConnection CreateSqlConnection(string connectionstring)
        {
            SqlConnection connection = new SqlConnection(connectionstring);
            connection.Open();

            if (connection != null && connection.State == System.Data.ConnectionState.Open)
                return connection;

            //
            throw new ApplicationException("SQL Verbindung ist NULL oder konnte nicht geöffnet werden!");
        }

        /// <summary>
        /// prüft konfigurationsdatei auf existens oder erzeugt diese bei bedarf
        /// </summary>
        /// <param name="path"></param>
        static void CheckIfPropertyFile(string path)
        {
            if (!File.Exists(path))
            {
                //
                Console.WriteLine("Es existiert keine Konfigurationsdatei. Soll eine leere Datei erzeugt werden [J] - DEFAULT [J]?");

                if (Console.ReadLine().ToUpper().Equals("J"))
                {
                    using (StreamWriter writer = new StreamWriter(path))
                    {
                        writer.WriteLine("{");
                        writer.WriteLine("  \"exportpath\":\"E:\\\\EEX\\\\EASY\\\\export\\\\Lieferscheine\",");
                        writer.WriteLine("  \"clienturl\":\"http://localhost:9090\",");
                        writer.WriteLine("  \"instance\":\"EEX\",");
                        writer.WriteLine("  \"clientuser\":\"superadmin\",");
                        writer.WriteLine("  \"clientpass\":\"super\",");
                        writer.WriteLine("  \"repository\":\"EssilorBelege\",");
                        writer.WriteLine("  \"schema\":\"EssilorBelege\",");
                        writer.WriteLine("  \"connectionstring\" :\"http://localhost:9090\"");
                        writer.WriteLine("}");
                    }
                }
                else
                    throw new ApplicationException("Ohne Konfigurationsdatei ist kein Export möglich! Erzeugen Sie eine Konfigurationsdatei.");
            }
        }

        /// <summary>
        /// parst die konfiguration und prüft auf gültigkeit
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        static EbisExporterProperties ReadAndParsePropertyFile(string path)
        {
            EbisExporterProperties properties = null;

            using (StreamReader reader = new StreamReader(path))
            {
                Type result = new EbisExporterProperties().GetType();
                Newtonsoft.Json.JsonSerializer serialize = new Newtonsoft.Json.JsonSerializer();
                properties = (EbisExporterProperties)serialize.Deserialize(reader, result);
            }

            if (properties != null)
            {
                // auf gültigkeit prüfen
                properties.CheckProperties();

                //
                return properties;
            }
                
            throw new ApplicationException("Konfiguration ist NULL!");
        }

        /// <summary>
        /// status des exportes
        /// </summary>
        enum Status
        {
            unbekannt = 0, pausiert = 1, gestartet = 2, abgeschlossen = 3, abgebrochen = 4, fehler = 5
        }

        #region events

        /// <summary>
        /// event nach dem der indexierungscommand abgeschlossen wurde
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void Indexcommand_StatementCompleted(object sender, StatementCompletedEventArgs e)
        {
        }

        /// <summary>
        /// event nach dem der blobindexierungscommand abgeschlossen wurde
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void Blobcommand_StatementCompleted(object sender, StatementCompletedEventArgs e)
        {
        }

        /// <summary>
        /// event nach dem der notizindexierungscommand abgeschlossen wurde
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void Notecommand_StatementCompleted(object sender, StatementCompletedEventArgs e)
        {
        }

        #endregion
    }
}
