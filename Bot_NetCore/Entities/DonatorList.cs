﻿using System;
using System.Collections.Generic;
using System.Xml.Linq;

namespace Bot_NetCore.Entities
{
    [Obsolete]
    public static class DonatorList //TODO: удалить после миграции
    {
        public static Dictionary<ulong, DonatorLegacy> Donators = new Dictionary<ulong, DonatorLegacy>();

        public static void SaveToXML(string fileName)
        {
            var doc = new XDocument();
            var root = new XElement("donators");

            foreach (var donator in Donators.Values)
            {
                var dElement = new XElement("donator");
                dElement.Add(new XElement("id", donator.Member));
                dElement.Add(new XElement("balance", donator.Balance));
                dElement.Add(new XElement("colorRole", donator.ColorRole));
                dElement.Add(new XElement("date", donator.Date.ToString("dd-MM-yyyy")));
                if (donator.Hidden) dElement.Add(new XElement("hidden", "True"));
                foreach (var friend in donator.Friends) dElement.Add(new XElement("friend", friend));
                root.Add(dElement);
            }

            doc.Add(root);
            doc.Save(fileName);
        }

        public static void ReadFromXML(string fileName)
        {
            var doc = XDocument.Load(fileName);
            foreach (var donator in doc.Element("donators").Elements("donator"))
            {
                var date = DateTime.Now.Date;
                if (donator.Element("date") != null) date = DateTime.ParseExact(donator.Element("date").Value, "dd-MM-yyyy", null);
                var created =
                    new DonatorLegacy(Convert.ToUInt64(donator.Element("id").Value),
                        Convert.ToUInt64(donator.Element("colorRole").Value),
                        date,
                        Convert.ToDouble(donator.Element("balance").Value));
                foreach (var friend in donator.Elements("friend")) created.AddFriend(Convert.ToUInt64(friend.Value));

                if (donator.Element("hidden") != null) created.UpdateHidden(true);
            }
        }
    }
}
