using System;
using System.Collections.Generic;
using System.Xml.Linq;
using ProxyStudio.Models;

namespace ProxyStudio.Helpers;

public class MpcFillXML
{
    /// This class is to obtain and parse xml from mpcfill.com
    /// 
        
        
    public MpcFillXML()
    {
        // Default constructor
    }

    public List<Card> ParseMyXML(string xmlContent)
    {
            


        if (string.IsNullOrEmpty(xmlContent))
        {
            throw new ArgumentException("XML content cannot be null or empty.", nameof(xmlContent));
        }
        // Parse the XML content and populate the _cards list
        // This is a placeholder for actual XML parsing logic
        // You would typically use an XML parser like System.Xml.Linq or System.Xml.Serialization here

        XDocument doc = XDocument.Parse(xmlContent);
        XElement? order = doc.Element("order");

        var details = order?.Element("details");
        var fronts = order?.Element("fronts");

        Order parsed = new Order
        {
            Quantity = int.Parse(order?.Element("quantity")?.Value ?? "0"),
            Bracket = int.Parse(order?.Element("bracket")?.Value ?? "0"),
            Stock = order?.Element("stock")?.Value ?? "Unknown",
            Foil = bool.Parse(order?.Element("foil")?.Value ?? "false"),
            CardBack = order?.Element("cardback")?.Value ?? "DefaultCardBack"

        };

        foreach (XElement card in fronts.Elements("card"))
        {
            string name = card.Element("name")?.Value ?? "Unknown";
            string id = card.Element("id")?.Value ?? "Unknown";
            string description = card.Element("description")?.Value ?? "No Description";
            string query = card.Element("query")?.Value ?? "Default Query";


            Card newCard = new Card(name, id, query)
            {
                Query = description,
                // _Width = int.Parse(card.Element("width")?.Value ?? "83"),
                // _Height = int.Parse(card.Element("height")?.Value ?? "118"),
                EnableBleed = bool.Parse(card.Element("bleedchecked")?.Value ?? "true")
            };
            parsed.Cards.Add(newCard);
        }




        return parsed.Cards;

    }





}

public class Order
{
    public int Quantity { get; set; }
    public int Bracket { get; set; }
    public string Stock { get; set; }
    public bool Foil { get; set; }
    public List<Card> Cards { get; set; } = new();
    public string CardBack { get; set; }
}