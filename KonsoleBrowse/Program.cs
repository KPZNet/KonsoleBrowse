﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Opc.Ua;   // Install-Package OPCFoundation.NetStandard.Opc.Ua
using Opc.Ua.Client;
using Opc.Ua.Configuration;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;


namespace KonsoleBrowse
{

    class Program
    {
        //private const string DiscoveryUrl = "opc.tcp://172.16.10.62:4846";
        //private const string DiscoveryUrl = "opc.tcp://DESKTOP-35D3VE0:48010";
        //private const string DiscoveryUrl = "opc.tcp://172.16.10.65:4846";
        private const string DiscoveryUrl = "opc.tcp://172.16.10.66:4846";


        static int lineCount = 0;
        static int totalNodes = 0;
        static int netCalls = 0;
        static DateTime startTime;
        static DateTime lastCheckTime;

        static BrowseResultCollection nodeList = new BrowseResultCollection();

        static void Main(string[] args)
        {
            
            Console.WriteLine("Create application configuration and certificate, start connection and extraction...");
            var config = new ApplicationConfiguration()
            {
                ApplicationName = "UABenchMarks",
                ApplicationUri = Utils.Format(@"urn:{0}:UABenchMarks", System.Net.Dns.GetHostName()),
                ApplicationType = ApplicationType.Client,
                SecurityConfiguration = new SecurityConfiguration
                {
                    ApplicationCertificate = new CertificateIdentifier { StoreType = @"Directory", StorePath = @"%CommonApplicationData%\OPC Foundation\CertificateStores\MachineDefault", SubjectName = Utils.Format(@"CN={0}, DC={1}", "MyHomework", System.Net.Dns.GetHostName()) },
                    TrustedIssuerCertificates = new CertificateTrustList { StoreType = @"Directory", StorePath = @"%CommonApplicationData%\OPC Foundation\CertificateStores\UA Certificate Authorities" },
                    TrustedPeerCertificates = new CertificateTrustList { StoreType = @"Directory", StorePath = @"%CommonApplicationData%\OPC Foundation\CertificateStores\UA Applications" },
                    RejectedCertificateStore = new CertificateTrustList { StoreType = @"Directory", StorePath = @"%CommonApplicationData%\OPC Foundation\CertificateStores\RejectedCertificates" },
                    AutoAcceptUntrustedCertificates = true,
                    AddAppCertToTrustedStore = true
                },
                TransportConfigurations = new TransportConfigurationCollection(),
                TransportQuotas = new TransportQuotas { OperationTimeout = 15000 },
                ClientConfiguration = new ClientConfiguration { DefaultSessionTimeout = 60000 },
                TraceConfiguration = new TraceConfiguration()
            };
            config.Validate(ApplicationType.Client).GetAwaiter().GetResult();
            if (config.SecurityConfiguration.AutoAcceptUntrustedCertificates)
            {
                config.CertificateValidator.CertificateValidation += (s, e) => { e.Accept = (e.Error.StatusCode == StatusCodes.BadCertificateUntrusted); };
            }

            var application = new ApplicationInstance
            {
                ApplicationName = "UABenchMarks",
                ApplicationType = ApplicationType.Client,
                ApplicationConfiguration = config
            };
            application.CheckApplicationInstanceCertificate(false, 2048).GetAwaiter().GetResult();

            var selectedEndpoint = CoreClientUtils.SelectEndpoint(DiscoveryUrl, useSecurity: false, operationTimeout: 15000);

            using (var session = Session.Create(config, new ConfiguredEndpoint(null, selectedEndpoint, EndpointConfiguration.Create(config)), false, "", 60000, null, null).GetAwaiter().GetResult())
            {
                Console.WriteLine("Trying QueryFirst Call...");
                //QueryFirstCall(session);

                Console.WriteLine("Start Fetch...");
                startTime = DateTime.Now;
                BrowseRootTree(session);
                DateTime end = DateTime.Now;
                TimeSpan ts = end - startTime;

                Console.WriteLine("Total Net Calls: {0}", netCalls);
                Console.WriteLine("Total Nodes Found: {0}", totalNodes);
                Console.WriteLine("Total Get Time: Seconds: {1}", ts.TotalMinutes, ts.TotalSeconds);


                //Console.WriteLine("Step 4 - Create a subscription. Set a faster publishing interval if you wish.");
                //var subscription = new Subscription(session.DefaultSubscription) { PublishingInterval = 30000 };

                //Console.WriteLine("Step 5 - Add a list of items you wish to monitor to the subscription.");
                //var list = new List<MonitoredItem> { new MonitoredItem(subscription.DefaultItem) { DisplayName = "ServerStatusCurrentTime", StartNodeId = "i=2258" } };
                //list.ForEach(i => i.Notification += OnNotification);
                //subscription.AddItems(list);

                //Console.WriteLine("Step 6 - Add the subscription to the session.");
                //session.AddSubscription(subscription);
                //subscription.Create();

                //Console.WriteLine("Press any key to remove subscription...");
                //Console.ReadKey(true);
            }

            Console.WriteLine("Press any key to exit...");
            Console.ReadKey(true);
        }
        private static void BrowseTree2(Session session, BrowseDescriptionCollection bdCollection, string sPre)
        {

            BrowseResultCollection br = new BrowseResultCollection();
            DiagnosticInfoCollection dc = new DiagnosticInfoCollection();

            netCalls++;
            Console.WriteLine($"Getting: {bdCollection.Count}");
            if (bdCollection.Count != 0)
            {
                session.Browse(null, null, (uint)1000000, bdCollection, out br, out dc);
                if (br != null)
                {
                    if (bdCollection.Count != br.Count)
                    {
                        Console.WriteLine($"NOT EQUAL:  DC Count = {dc.Count}");
                    }
                    int i = 0;
                    BrowseDescriptionCollection bc = new BrowseDescriptionCollection();
                    foreach (var b in br)
                    {
                        nodeList.Add(b);
                        var inNode = bdCollection[i];
                        foreach (var n in b.References)
                        {
                            BrowseDescription bd = new BrowseDescription();
                            bd.NodeId = (NodeId)n.NodeId;
                            bd.BrowseDirection = BrowseDirection.Forward;
                            bd.ReferenceTypeId = ReferenceTypeIds.HasChild;
                            bd.IncludeSubtypes = true;
                            bd.NodeClassMask = (uint)NodeClass.Variable | (uint)NodeClass.Object | (uint)NodeClass.Method;
                            bc.Add(bd);
                        }
                    }
                    BrowseTree2(session, bc, "KN");

                }
            }
        }

        private static void BrowseRootTree2(Session session)
        {
            Console.WriteLine("Browse the server namespace.");
            ReferenceDescriptionCollection refs;
            Byte[] cp;
            netCalls++;
            session.Browse(null, null, ObjectIds.ObjectsFolder, 0u, BrowseDirection.Forward, ReferenceTypeIds.HierarchicalReferences, true, (uint)NodeClass.Variable | (uint)NodeClass.Object | (uint)NodeClass.Method, out cp, out refs);
            Console.WriteLine("DisplayName: BrowseName, NodeClass");

            BrowseDescriptionCollection bc = new BrowseDescriptionCollection();
            foreach (var rd in refs)
            {
                totalNodes++;
                Console.WriteLine("{0}: {1}, {2}", rd.DisplayName, rd.BrowseName, rd.NodeClass);

                BrowseDescription bd = new BrowseDescription();
                bd.NodeId = (NodeId)rd.NodeId;
                bd.BrowseDirection = BrowseDirection.Forward;
                bd.ReferenceTypeId = ReferenceTypeIds.HierarchicalReferences;
                bd.IncludeSubtypes = true;
                bd.NodeClassMask = (uint)NodeClass.Variable | (uint)NodeClass.Object | (uint)NodeClass.Method;
                bc.Add(bd);
            }
            BrowseTree2(session, bc, "");

            Console.WriteLine($"Total Nodes: {nodeList.Count}");
        }

        private static void BrowseTree(Session session, ReferenceDescription reff, string sPre)
        {
            NodeId propTypeId = ExpandedNodeId.ToNodeId(68, session.NamespaceUris);
            ReferenceDescriptionCollection nextRefs;
            byte[] nextCp;
            netCalls++;
            session.Browse(null, null, ExpandedNodeId.ToNodeId(reff.NodeId, session.NamespaceUris), 0u, BrowseDirection.Forward, ReferenceTypeIds.HierarchicalReferences, true, (uint)NodeClass.Variable | (uint)NodeClass.Object | (uint)NodeClass.Method, out nextCp, out nextRefs);
            if (nextRefs != null)
            {
                foreach (var nextRd in nextRefs)
                {
                    totalNodes++;
                    if (lineCount % 500 == 0)
                    {
                        DateTime end = DateTime.Now;
                        

                        TimeSpan ts = end - startTime;
                        TimeSpan lastCallTS = end - lastCheckTime;

                        //Console.WriteLine("Total Net Calls: {0}", netCalls);
                        Console.WriteLine("Total Get Time: Seconds: {1} :  Since Last: {0}", lastCallTS.TotalSeconds, ts.TotalSeconds);
                        Console.WriteLine("Node Sample Count: {4} : {1}", nextRd.DisplayName, nextRd.BrowseName, nextRd.NodeClass, sPre, lineCount);
                        Console.WriteLine("");

                        lastCheckTime = DateTime.Now;
                    }
                    lineCount++;
                     
                    if (nextRd.NodeClass == NodeClass.Variable || nextRd.NodeClass == NodeClass.Object)
                    {                    
                        if (nextRd.TypeDefinition != propTypeId)
                        {
                            BrowseTree(session, nextRd, sPre += "-");
                        }
                        else
                        {
                            //Console.WriteLine("<<< SKIPPING Property >>>");
                        }
                    }

                }
            }
        }

        private static void QueryFirstCall(Session session)
        {

            ViewDescription vd = new ViewDescription();
            vd.ViewId = 5000;
            vd.ViewVersion = 0;
            NodeTypeDescriptionCollection nt = new NodeTypeDescriptionCollection();
            NodeTypeDescription ntd = new NodeTypeDescription();

            ExpandedNodeId enid = new ExpandedNodeId("ns=3;s=AirConditioner_1");

            ntd.TypeDefinitionNode = enid;
            nt.Add(ntd);

            ContentFilterElement cfe = new ContentFilterElement();
            ContentFilterElementCollection cfec = new ContentFilterElementCollection();


            cfec.Add(cfe);
            ContentFilter cf = new ContentFilter();
            cf.Elements = cfec;

            QueryDataSetCollection qdsc = new QueryDataSetCollection();
            byte[] cp = new byte[100];
            ParsingResultCollection prc = new ParsingResultCollection();
            DiagnosticInfoCollection dic = new DiagnosticInfoCollection();
            ContentFilterResult cfr = new ContentFilterResult();

            try
            {
                var rs = session.QueryFirst(null, vd, nt, cf, 1000, 1000, out qdsc, out cp, out prc, out dic, out cfr);
            }
            catch(Exception eX)
            {
                Console.WriteLine("EXCEPTION:  QueryFirst  :  {0}", eX.Message);
            }
        }

        private static void BrowseRootTree(Session session)
        {
            Console.WriteLine("Browse the server namespace.");
            ReferenceDescriptionCollection refs;
            Byte[] cp;
            netCalls++;
            session.Browse(null, null, ObjectIds.ObjectsFolder, 0u, BrowseDirection.Forward, ReferenceTypeIds.HierarchicalReferences, true, (uint)NodeClass.Variable | (uint)NodeClass.Object | (uint)NodeClass.Method, out cp, out refs);
            Console.WriteLine("DisplayName: BrowseName, NodeClass");
            foreach (var rd in refs)
            {
                totalNodes++;
                Console.WriteLine("Root: {0}: {1}, {2}", rd.DisplayName, rd.BrowseName, rd.NodeClass);
                BrowseTree(session, rd, "");
            }
        }

        private static void OnNotification(MonitoredItem item, MonitoredItemNotificationEventArgs e)
        {
            foreach (var value in item.DequeueValues())
            {
                //Console.WriteLine("{0}: {1}, {2}, {3}", item.DisplayName, value.Value, value.SourceTimestamp, value.StatusCode);
            }
        }

    }

}

