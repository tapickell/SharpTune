/*
    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.
*/
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.Globalization;
using System.IO;
using System.Diagnostics;
using System.Xml;
using System.Xml.XPath;
using System.Xml.Linq;
using System.Linq;
using SharpTune;
using SharpTune.Properties;
using SharpTune.Core;
using SharpTune.EcuMapTools;
using SharpTune.RomMod;
using System.Runtime.Serialization;
using System.Reflection;

namespace SharpTuneCore
{
    
    /// <summary>
    /// Represents an individual device definition
    /// Includes ALL scalings from base to top
    /// </summary>
    /// 
    [Serializable]
    public class Definition
    {
        public bool isBase { get; private set; }
        public string internalId { get; set; }
        public int internalIdAddress { get; private set; }                

        private Dictionary<string,string> carInfo;
        /// <summary>
        /// Contains basic info for the definition
        /// Make, Model, etc
        /// </summary>
        public Dictionary<string, string> CarInfo {
            get { return carInfo; } 
            private set{
                    carInfo = value;
                    CreateXRomId();
           }
        }

        /// <summary>
        /// Contains the file path to the XML definition (top)
        /// </summary>
        public string defPath { get; set; }

        public string include { get; set; }
        /// <summary>
        /// Holds all XElements pulled from XML for ROM tables
        /// Includes inherited XML
        /// </summary>
        public XElement xRomId { get; set; }

        public Dictionary<string,Table> ExposedRomTables { get; private set;}
        public Dictionary<string,Table> ExposedRamTables { get; private set;}
        
        public Dictionary<string,Table> InheritedExposedRomTables { 
            get{
                Dictionary<string,Table> ret = new Dictionary<string,Table>();
                foreach(Definition d in inheritList)
                {
                    foreach(Table t in d.ExposedRomTables.Values)
                    {
                        if(!ret.ContainsKey(t.name))
                            ret.Add(t.name,t);
                    }
                }
                return ret;
            }
            private set{}
        }

        public Dictionary<string,Table> InheritedExposedRamTables { 
            get{
                Dictionary<string,Table> ret = new Dictionary<string,Table>();
                foreach(Definition d in inheritList)
                {
                    foreach(Table t in d.ExposedRamTables.Values)
                    {
                        if(!ret.ContainsKey(t.name))
                            ret.Add(t.name,t);
                    }
                }
                return ret;
            }
            private set{}
        }

        public Dictionary<string, Table> AggregateExposedRomTables { 
            get{
                return Utils.AggregateDictionary(ExposedRomTables,InheritedExposedRomTables);
            }
            private set{}
        }
        public Dictionary<string, Table> AggregateExposedRamTables { 
            get{
                return Utils.AggregateDictionary(ExposedRamTables,InheritedExposedRamTables);
            }
            private set{}
        }
        public Dictionary<string,Table> BaseRomTables { get; private set;}
        public Dictionary<string,Table> BaseRamTables { get; private set;}

        public Dictionary<string,Table> InheritedBaseRomTables { 
            get{
                Dictionary<string,Table> ret = new Dictionary<string,Table>();
                foreach(Definition d in inheritList)
                {
                    foreach(Table t in d.BaseRomTables.Values)
                    {
                        if(!ret.ContainsKey(t.name))
                            ret.Add(t.name,t);
                    }
                }
                return ret;
            }
            private set{}
        }

        public Dictionary<string,Table> InheritedBaseRamTables { 
            get{
                Dictionary<string,Table> ret = new Dictionary<string,Table>();
                foreach(Definition d in inheritList)
                {
                    foreach(Table t in d.BaseRamTables.Values)
                    {
                        if(!ret.ContainsKey(t.name))
                            ret.Add(t.name,t);
                    }
                }
                return ret;
            }
            private set{}
        }

        public Dictionary<string, Table> AggregateBaseRomTables { 
            get{
                return Utils.AggregateDictionary(BaseRomTables,InheritedBaseRomTables);
            }
            private set{}
        }
        public Dictionary<string, Table> AggregateBaseRamTables { 
            get{
                return Utils.AggregateDictionary(BaseRamTables,InheritedBaseRamTables);
            }
            private set{}
        }

        public List<Table> RomTables{
            get{
                List<Table> tlist = new List<Table>();
                tlist.AddRange(ExposedRomTables.Values);
                tlist.AddRange(BaseRomTables.Values);
                return tlist;
            }
            private set{}
        }

        public List<Table> RamTables{
            get{
                List<Table> tlist = new List<Table>();
                tlist.AddRange(ExposedRomTables.Values);
                tlist.AddRange(BaseRomTables.Values);
                return tlist;
            }
            private set{}
        }

        public Dictionary<string, Scaling> ScalingList { get; private set; }

        public List<Definition> inheritList { get; private set; }

        /// <summary>
        /// Constructor
        /// </summary>
        public Definition()
        {
            isBase = false;
            CarInfo = new Dictionary<string, string>();
            ExposedRomTables = new Dictionary<string, Table>();
            ExposedRamTables = new Dictionary<string, Table>();
            BaseRomTables = new Dictionary<string, Table>();
            BaseRamTables = new Dictionary<string, Table>();
            ScalingList = new Dictionary<string,Scaling>();
            inheritList = new List<Definition>();
            include = null;
        }

        /// <summary>
        /// Constructor
        /// TODO: Change to a factory to share already-opened definitions
        /// TODO: Include more information about inheritance in the class for def-editing
        /// </summary>
        /// <param name="calID"></param>
        public Definition(string filepath)
            : this()
        {
            internalId = null;
            defPath = filepath;
            LoadRomId();
        }

        public Definition(string respath, bool isres)
            : this()
        {
            internalId = null;
            defPath = respath;
            LoadRomId();
        }

        /// <summary>
        /// Constructor used to create new definitions using existing data
        /// </summary>
        /// <param name="filepath"></param>
        /// <param name="carinfo"></param>
        /// <param name="include"></param>
        /// <param name="xromt"></param>
        /// <param name="xramt"></param>
        public Definition(string filepath, Dictionary<string, string> carinfo, string incl) : this()
        {
            //TODO error handling
            include = incl;
            CarInfo = new Dictionary<string,string>(carinfo);
            internalId = carinfo["internalidstring"];
            internalIdAddress = int.Parse(carinfo["internalidaddress"], System.Globalization.NumberStyles.AllowHexSpecifier);
            defPath = filepath;
            Inherit();
        }

        /// <summary>
        /// Constructor used to create new definitions using existing data
        /// </summary>
        /// <param name="filepath"></param>
        /// <param name="carinfo"></param>
        /// <param name="include"></param>
        /// <param name="xromt"></param>
        /// <param name="xramt"></param>
        public Definition(string filepath, Mod mod) : this()
        {
            this.include = mod.InitialCalibrationId;
            defPath = filepath;
            
            CloneRomIdFrom(include,true);

            CarInfo["internalidaddress"] = mod.ModIdentAddress.ToString("X");
            CarInfo["internalidstring"] = mod.ModIdent.ToString();
            CarInfo["ecuid"] = mod.FinalEcuId.ToString();
            CarInfo["xmlid"] = mod.ModIdent.ToString();
            Inherit();

            //TODO: ADD THE TABLES FROM MOD
            //mod.modDef.
            foreach (var rt in mod.modDef.RomLutList)
            {
                //do something with each lut.
                ExposeTable(rt.Name, rt); //TODO: Fix this redundancy?
            }
        }

        /// <summary>
        /// Clones an existing definitions romid
        /// TODO: make the unknown rom handler use this.
        /// </summary>
        /// <param name="inh"></param>
        /// device identifier to clone from
        /// <param name="incl"></param>
        /// include the device (true) or include its base (false)
        /// <returns></returns>
        public bool CloneRomIdFrom(string inh, bool incl)
        {
            if(!SharpTuner.AvailableDevices.DefDictionary.ContainsKey(inh))
                return false;
            Definition d = SharpTuner.AvailableDevices.DefDictionary[inh];
            CarInfo = new Dictionary<string, string>(d.CarInfo);
            if (incl)
                include = d.internalId;
            else
                include = d.include;
            return true;
        }


        public void ParseRomId()
        {
            CarInfo.Clear();
            foreach (XElement element in xRomId.Elements())
            {
                CarInfo.Add(element.Name.ToString(), element.Value.ToString());
            }
            if (CarInfo.ContainsKey("internalidstring"))
                internalId = CarInfo["internalidstring"];
            if (CarInfo.ContainsKey("internalidaddress"))
                internalIdAddress = int.Parse(CarInfo["internalidaddress"], System.Globalization.NumberStyles.AllowHexSpecifier);
            if (CarInfo.ContainsKey("xmlid"))
                if (CarInfo["xmlid"].Contains("BASE"))
                    internalId = CarInfo["xmlid"].ToString();
        }

        /// <summary>
        /// Read the rom identification header and include from a file
        /// </summary>
        /// <param name="fetchPath"></param>
        /// <returns></returns>
        public void LoadRomId()
        {
            
            XDocument xmlDoc = XDocument.Load(defPath, LoadOptions.PreserveWhitespace);
            this.xRomId = xmlDoc.XPathSelectElement("/rom/romid");
            ParseRomId();
            if (xmlDoc.XPathSelectElement("/rom/include") != null)
                include = xmlDoc.XPathSelectElement("/rom/include").Value.ToString();
        }

        public XElement ExportRRRomId()
        {
            XElement xe = new XElement("rom");
            xe.SetAttributeValue("base",this.include);
            XElement xeromid = new XElement("romid");
            foreach(KeyValuePair<String,String> entry in carInfo){
                XElement xel = new XElement(entry.Key.ToString(),entry.Value.ToString());
                xeromid.Add(xel);
            }
            if (!carInfo.ContainsKey("filesize") && carInfo.ContainsKey("memmodel"))
            {
                if (carInfo["memmodel"].ToLower() == "sh7055")
                    xeromid.Add(new XElement("filesize", "512kb"));
                else if (carInfo["memmodel"].ToLower() == "sh7058")
                    xeromid.Add(new XElement("filesize", "1024kb"));
                else if (carInfo["memmodel"].ToLower() == "68hc16ys")
                    xeromid.Add(new XElement("filesize", "192kb"));
            }
            xe.Add(xeromid);
            return xe;
        }

        /// <summary>
        /// Populates a 'short def' (romid + include) into a full definition
        /// </summary>
        /// <returns></returns>
        public bool Populate()
        {
            if (internalId != null && defPath != null)
            {
                Clear();
                if (include != null)
                    Inherit();
                if (ReadXML(defPath) && include != null)
                    return true;
            }
            return false;
        }

        private void Inherit()
        {
            Dictionary<string, Definition> dd = SharpTuner.AvailableDevices.DefDictionary;
            if (dd.ContainsKey(include) && dd[include].internalId != null)
                dd[include].Populate();

            if(SharpTuner.AvailableDevices.DefDictionary[include].inheritList.Count > 0)
                inheritList.AddRange(SharpTuner.AvailableDevices.getDef(include).inheritList);

            inheritList.Add(SharpTuner.AvailableDevices.DefDictionary[include]);
            inheritList.Reverse();
        }

        private void Clear()
        {
            ExposedRomTables.Clear();
            ExposedRamTables.Clear();
            BaseRomTables.Clear();
            BaseRamTables.Clear();
            ScalingList.Clear();
            inheritList.Clear();
        }

        /// <summary>
        /// Load parameters from XML an XML file
        /// </summary>
        public bool ReadXML(string path)
        {
            if (path == null) return false;
            XDocument xmlDoc = XDocument.Load(path, LoadOptions.PreserveWhitespace);
            // ROM table fetches here!
            var tableQuery = from t in xmlDoc.XPathSelectElements("/rom/table")
                             select t;
            foreach (XElement table in tableQuery)
            {
                if (table.Attribute("name") == null) 
                    continue;
                string tablename = table.Attribute("name").Value.ToString();
                AddRomTable(TableFactory.CreateTable(table,this));
            }
            // RAM table feteches here!
            var ramtableQuery = from t in xmlDoc.XPathSelectElements("/ram/table")
                                select t;
            foreach (XElement table in ramtableQuery)
            {
                if (table.Attribute("name") == null) 
                    continue;
                string tablename = table.Attribute("name").Value.ToString();

                AddRamTable(TableFactory.CreateTable(table,this));
            }
            //Read Scalings
            var scalingQuery = from sc in xmlDoc.XPathSelectElements("/rom/scaling")
                               //where table.Ancestors("table").First().IsEmpty
                               select sc;
            foreach (XElement scaling in scalingQuery)
            {
                //skip scalings with no name
                if (scaling.Attribute("name") == null) continue;
                string scalingname = scaling.Attribute("name").Value.ToString();
                if (!this.ScalingList.ContainsKey(scalingname))
                {
                    this.ScalingList.Add(scalingname, ScalingFactory.CreateScaling(scaling));
                }
            }
            return true;
        }

        private void AddRomTable(Table table)
        {
            if (table.isBase)
            {
                if (!BaseRomTables.ContainsKey(table.name))
                    BaseRomTables.Add(table.name, table);
                else
                    Trace.WriteLine("Warning, duplicate table: " + table.name + ". Please check the definition!!");
            }
            else
            {
                if (!ExposedRomTables.ContainsKey(table.name))
                    ExposedRomTables.Add(table.name, table);
                else
                    Trace.WriteLine("Warning, duplicate table: " + table.name + ". Please check the definition!!");
            }
        }

        private void AddRamTable(Table table)
        {
            if (table.isBase)
            {
                if (!BaseRamTables.ContainsKey(table.name))
                    BaseRamTables.Add(table.name, table);
                else
                    Trace.WriteLine("Warning, duplicate table: " + table.name + ". Please check the definition!!");
            }
            else
            {
                if (!ExposedRamTables.ContainsKey(table.name))
                    ExposedRamTables.Add(table.name, table);
                else
                    Trace.WriteLine("Warning, duplicate table: " + table.name + ". Please check the definition!!");
                    
            }
        }

        /// <summary>
        /// Pulls the scaling xelement from the definition at fetchPath
        /// </summary>
        /// <returns>
        /// The scalings.
        /// </returns>
        /// <param name='fetchPath'>
        /// Fetch path.
        /// </param>
        public static void pullScalings(String fetchPath, ref List<XElement> xbs, ref List<XElement> xs)
        {

            if (fetchPath == null) return;
            List<XElement> xlist = new List<XElement>();
            XDocument xmlDoc = XDocument.Load(fetchPath, LoadOptions.PreserveWhitespace);
            var scalingQuery = from sc in xmlDoc.XPathSelectElements("/rom/scaling")
                               //where table.Ancestors("table").First().IsEmpty
                               select sc;
            foreach (XElement scaling in scalingQuery)
            {
                if (scaling.Attribute("storagetype") != null && scaling.Attribute("storagetype").Value == "bloblist")
                {
                    scaling.Attribute("storagetype").Remove();
                    xbs.Add(scaling);
                }
                else
                {
                    xs.Add(scaling);
                }
            }
            scalingQuery.ToList().ForEach(x => x.Remove());
            using (XmlTextWriter xmlWriter = new XmlTextWriter(fetchPath, new UTF8Encoding(false)))
            {
                xmlWriter.Formatting = Formatting.Indented;
                xmlWriter.Indentation = 4;
                xmlDoc.Save(xmlWriter);
            }
        }

        /// <summary>
        /// Load parameters from XML an XML file
        /// </summary>
        public static void ConvertXML(string fetchPath, ref List<String> blobtables,
            ref Dictionary<String, List<String>> t3d,
            ref Dictionary<String, List<String>> t2d,
            ref Dictionary<String, List<String>> t1d,
            Dictionary<String, String> imap,
            bool isbase)
        {

            if (fetchPath == null) return;
            XDocument xmlDoc = XDocument.Load(fetchPath, LoadOptions.PreserveWhitespace);
            List<String> newtables = new List<String>();
            String rombase;

            Dictionary<String, List<String>> includes = new Dictionary<String, List<String>>();


            if (!isbase)
            {
                rombase = imap[fetchPath];
            }
            else
            {
                var xi = xmlDoc.XPathSelectElement("/rom/romid/xmlid");
                rombase = xi.Value.ToString();
            }

            // ROM table fetches here!
            var tableQuery = from t in xmlDoc.XPathSelectElements("/rom/table")
                             //where table.Ancestors("table").First().IsEmpty
                             select t;
            foreach (XElement table in tableQuery)
            {
                //skip tables with no name
                if (table.Attribute("name") == null) continue;
                foreach (String bt in blobtables)
                {
                    if ((table.Attribute("scaling") != null && table.Attribute("scaling").Value == bt) || (table.Attribute("name") != null && table.Attribute("name").Value == bt))
                    {
                        table.Name = "tableblob";

                        if (isbase)
                            newtables.Add(table.Attribute("name").Value);

                        if (table.Attribute("type") != null)
                            table.Attribute("type").Remove();
                        break;
                    }
                }
                if (isbase)
                {
                    blobtables.AddRange(newtables);
                    newtables.Clear();
                }

                if (table.Name == "tableblob")
                {
                    continue;
                }

                bool xaxis = false;
                bool yaxis = false;

                foreach (XElement xel in table.Descendants())
                {
                    if (xel.Name == "table")
                    {
                        if (xel.Attribute("name") != null && xel.Attribute("name").Value == "X")
                        {
                            xel.Name = "xaxis";
                            xel.Attribute("name").Remove();
                            xaxis = true;
                        }
                        else if (xel.Attribute("type") != null && xel.Attribute("type").Value.ContainsCI("static x axis"))
                        {
                            xel.Name = "staticxaxis";
                            xel.Attribute("type").Remove();
                            xaxis = true;
                        }
                        else if (xel.Attribute("type") != null && xel.Attribute("type").Value.ContainsCI("x axis"))
                        {
                            xel.Name = "xaxis";
                            xel.Attribute("type").Remove();
                            xaxis = true;
                        }
                        else if (xel.Attribute("name") != null && xel.Attribute("name").Value == "Y")
                        {
                            xel.Name = "yaxis";
                            xel.Attribute("name").Remove();
                            yaxis = true;
                        }
                        else if (xel.Attribute("type") != null && xel.Attribute("type").Value.ContainsCI("static y axis"))
                        {
                            xel.Name = "staticyaxis";
                            xel.Attribute("type").Remove();
                            yaxis = true;
                        }
                        else if (xel.Attribute("type") != null && xel.Attribute("type").Value.ContainsCI("y axis"))
                        {
                            xel.Name = "yaxis";
                            xel.Attribute("type").Remove();
                            yaxis = true;
                        }

                    }
                }

                if (!isbase)
                {
                    if (t3d[rombase].Contains(table.Attribute("name").Value.ToString()))
                    {
                        table.Name = "table3d";
                        if (table.Attribute("type") != null) table.Attribute("type").Remove();
                        continue;
                    }
                    if (t2d[rombase].Contains(table.Attribute("name").Value.ToString()))
                    {
                        table.Name = "table2d";
                        if (table.Attribute("type") != null) table.Attribute("type").Remove();
                        continue;
                    }
                    if (t1d[rombase].Contains(table.Attribute("name").Value.ToString()))
                    {
                        table.Name = "table1d";
                        if (table.Attribute("type") != null) table.Attribute("type").Remove();
                        continue;
                    }
                }
                if (xaxis && yaxis) table.Name = "table3d";
                else if (xaxis || yaxis) table.Name = "table2d";
                else table.Name = "table1d";
                if (table.Attribute("type") != null) table.Attribute("type").Remove();
                if (isbase)
                {
                    switch (table.Name.ToString())
                    {
                        case "table3d":
                            t3d[rombase].Add(table.Attribute("name").Value);
                            break;
                        case "table2d":
                            t2d[rombase].Add(table.Attribute("name").Value);
                            break;
                        case "table1d":
                            t1d[rombase].Add(table.Attribute("name").Value);
                            break;
                        default:
                            break;
                    }
                }

            }
            using (XmlTextWriter writer = new XmlTextWriter(fetchPath, new UTF8Encoding(false)))
            {
                writer.Formatting = Formatting.Indented;
                writer.Indentation = 4;
                xmlDoc.Save(writer);
            }
        }

        public bool ExportXML()
        {
            return ExportXML(this.defPath);
        }

        public bool ExportXML(string filepath)
        {
            try
            {
                XmlWriterSettings objXmlWriterSettings = new XmlWriterSettings();
                objXmlWriterSettings.Indent = true;
                objXmlWriterSettings.OmitXmlDeclaration = false;
                using (XmlWriter writer = XmlWriter.Create(filepath, objXmlWriterSettings))
                {
                    //Start writing doc
                    writer.WriteStartDocument();

                    //Write romid elements
                    //TODO THIS IS REDUNDANT
                    writer.WriteStartElement("rom");
                    writer.WriteStartElement("romid");
                    foreach (KeyValuePair<string, string> kvp in this.CarInfo)
                    {
                        writer.WriteElementString(kvp.Key.ToString(), kvp.Value.ToString());
                    }
                    writer.WriteEndElement();

                    //Write include
                    if (this.include != null)
                        writer.WriteElementString("include", this.include.ToString());

                    //Write scalings

                    if (this.ScalingList != null)
                    {
                        foreach (KeyValuePair<string, Scaling> table in this.ScalingList)
                        {
                            table.Value.xml.WriteTo(writer);
                        }
                    }
                    //Write ROM tables
                    if (RomTables != null)
                    {
                        List<Table> romExportList = (from entry in RomTables orderby entry.category ascending select entry)
                            .ToList();

                        foreach (Table table in romExportList)
                        {
                            table.xml.WriteTo(writer);
                        }
                    }
                    writer.WriteEndDocument();
                }
                return true;
            }
            catch (Exception e)
            {
                Trace.WriteLine(e.Message);
                return false;
            }
        }

        public void CreateXRomId()
        {
            XElement x = new XElement("romid");
            foreach (KeyValuePair<string, string> kvp in this.CarInfo)
            {
                x.Add(new XElement(kvp.Key.ToString(), kvp.Value.ToString()));
            }
            xRomId = x;
        }

        private Table GetBaseTable(string name)
        {
            foreach (Definition d in inheritList)
            {
                if (SharpTuner.AvailableDevices.DefDictionary[d.internalId].AggregateBaseRomTables.ContainsKey(name))
                    return SharpTuner.AvailableDevices.DefDictionary[d.internalId].AggregateBaseRomTables[name];
                else if (SharpTuner.AvailableDevices.DefDictionary[d.internalId].AggregateBaseRamTables.ContainsKey(name))//TODO FIX RAMTABLES
                    return SharpTuner.AvailableDevices.DefDictionary[d.internalId].AggregateBaseRamTables[name];
            }
            Trace.WriteLine("Warning: base table for " + name + " not found");
            return null;
        }

        public void ExposeTable(string name, Lut lut)
        {
            Table baseTable = GetBaseTable(name);
            if (baseTable != null)
            {

                Table childTable = baseTable.CreateChild(lut, this);
                //TODO: HANDLE STATIC AXES!!
                if (lut.dataAddress < 0x400000)
                {
                    //TODO: HANDLE UPDATES TO EXISTING TABLES!!??
                    if (ExposedRomTables.ContainsKey(childTable.name))
                        ExposedRomTables.Remove(childTable.name);
                    ExposedRomTables.Add(childTable.name, childTable);
                }
                else
                {
                    if (ExposedRamTables.ContainsKey(childTable.name))
                        ExposedRamTables.Remove(childTable.name);
                    ExposedRamTables.Add(childTable.name, childTable);
                }
            }

            //if (bt == null) return;
            //bt.SetAttributeValue("address", lut.dataAddress.ToString("X"));//(System.Int32.Parse(temptable.Value.Attribute("offset").Value.ToString(), System.Globalization.NumberStyles.AllowHexSpecifier) + offset).ToString("X"));
            //IEnumerable<XAttribute> tempattr = bt.Attributes();
            //List<String> remattr = new List<String>();
            //foreach (XAttribute attr in tempattr)
            //{
            //    if (attr.Name != "address" && attr.Name != "name")
            //    {
            //        remattr.Add(attr.Name.ToString());
            //    }
            //}
            //foreach (String rem in remattr)
            //{
            //    bt.Attribute(rem).Remove();
            //}

            //List<String> eleremlist = new List<String>();

            //foreach (XElement ele in bt.Elements())
            //{
            //    IEnumerable<XAttribute> childtempattr = ele.Attributes();
            //    List<String> childremattr = new List<String>();

            //    if (ele.Name.ToString() != "table")
            //    {
            //        eleremlist.Add(ele.Name.ToString());
            //        continue;
            //    }
            //    if (ele.Attribute("type").Value.ContainsCI("static"))
            //    {
            //        eleremlist.Add(ele.Name.ToString());
            //    }
            //    else if (ele.Attribute("type").Value.ContainsCI("x axis"))
            //    {
            //        ele.Attribute("name").Value = "X";
            //    }
            //    else if (ele.Attribute("type").Value.ContainsCI("y axis"))
            //    {
            //        ele.Attribute("name").Value = "Y";
            //    }
            //    foreach (XAttribute attr in childtempattr)
            //    {
            //        if (attr.Name != "address" && attr.Name != "name")
            //        {
            //            childremattr.Add(attr.Name.ToString());
            //        }
            //    }
            //    foreach (String rem in childremattr)
            //    {
            //        ele.Attribute(rem).Remove();
            //    }
            //}
            //foreach (String rem in eleremlist)
            //{
            //    bt.Element(rem).Remove();
            //}

        }


        ///// <summary>
        ///// Creates a table XEL from the template file, adding proper addresses
        ///// </summary>
        ///// <param name="name"></param>
        ///// <param name="offset"></param>
        ///// <returns></returns>
        //public void ExposeTable(string name, Lut3D lut) //int offset)
        //{
        //    XElement bt = GetTableBase(name);
        //    if (bt == null) return;
        //    bt.SetAttributeValue("address", lut.dataAddress.ToString("X"));
        //    IEnumerable<XAttribute> tempattr = bt.Attributes();
        //    List<String> remattr = new List<String>();
        //    foreach (XAttribute attr in tempattr)
        //    {
        //        if (attr.Name != "address" && attr.Name != "name")
        //        {
        //            remattr.Add(attr.Name.ToString());
        //        }
        //    }
        //    foreach (String rem in remattr)
        //    {
        //        bt.Attribute(rem).Remove();
        //    }

        //    List<String> eleremlist = new List<String>();

        //    foreach (XElement ele in bt.Elements())
        //    {
        //        IEnumerable<XAttribute> childtempattr = ele.Attributes();
        //        List<String> childremattr = new List<String>();

        //        if (ele.Name.ToString() != "table")
        //        {
        //            eleremlist.Add(ele.Name.ToString());
        //            continue;
        //        }
        //        if (ele.Attribute("type").Value.ContainsCI("static"))
        //        {
        //            eleremlist.Add(ele.Name.ToString());
        //        }
        //        else if (ele.Attribute("type").Value.ContainsCI("x axis"))
        //        {
        //            ele.Attribute("name").Value = "X";
        //            ele.SetAttributeValue("address", lut.colsAddress.ToString("X"));
        //        }
        //        else if (ele.Attribute("type").Value.ContainsCI("y axis"))
        //        {
        //            ele.Attribute("name").Value = "Y";
        //            ele.SetAttributeValue("address", lut.rowsAddress.ToString("X"));
        //        }
        //        foreach (XAttribute attr in childtempattr)
        //        {
        //            if (attr.Name != "address" && attr.Name != "name")
        //            {
        //                childremattr.Add(attr.Name.ToString());
        //            }
        //        }
        //        foreach (String rem in childremattr)
        //        {
        //            ele.Attribute(rem).Remove();
        //        }
        //    }
        //    foreach (String rem in eleremlist)
        //    {
        //        bt.Element(rem).Remove();
        //    }
        //    if (lut.dataAddress < 0x400000)
        //    {
        //        RomTableList.Add(name, TableFactory.CreateTable(bt));
        //    }
        //    else
        //    {
        //        RamTableList.Add(name, TableFactory.CreateTable(bt));
        //    }
        //}

        public void CopyTables(Definition d)
        {
            ExposedRomTables = new Dictionary<string,Table>(d.ExposedRomTables);
            ExposedRamTables = new Dictionary<string,Table>(d.ExposedRamTables);
            BaseRomTables = new Dictionary<string, Table>(d.BaseRomTables);
            BaseRamTables = new Dictionary<string, Table>(d.BaseRamTables);
            ScalingList = new Dictionary<string, Scaling>(d.ScalingList);
        }

        #region ECUFlash XML Code
        public void ImportMapFile(string filepath, DeviceImage image)
        {
            EcuMap im = new EcuMap();
            im.ImportFromMapFileOrText(filepath);
            ReadMap(im,image);
        }

        public void ImportMapText(string text, DeviceImage image)
        {
            EcuMap im = new EcuMap();
            im.ImportFromMapFileOrText(text);
            ReadMap(im,image);
        }

        public void ReadMap(EcuMap idaMap,DeviceImage image)
        {
            //loop through base def and search for table names in map
            foreach (var romtable in AggregateBaseRomTables)
            {
                foreach (var idan in idaMap.CleanLocs)
                {
                    if (romtable.Key.EqualsDefineString(idan.Key))
                    {
                        ExposeTable(romtable.Key, LutFactory.CreateLut(romtable.Key, uint.Parse(idan.Value.ToString(), NumberStyles.AllowHexSpecifier), image.imageStream));
                        break;
                    }
                }
            }
            ////TODO RAMTABLES
            //foreach (var ramtable in baseDef.RamTableList)
            //{
            //    foreach (var idan in idaMap.IdaCleanNames)
            //    {
            //        if (ramtable.Key.EqualsIdaString(idan.Key))
            //        {
            //            break;
            //        }
            //    }
            //}
        }

        #endregion
    }
}
