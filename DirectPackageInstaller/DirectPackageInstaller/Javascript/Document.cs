using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Dynamic;
using System.Linq;
using System.Text.Json.Serialization;
using Avalonia.Controls;
using HtmlAgilityPack;
using Jint;
using Jint.Native;
using Jint.Native.Object;
using Jint.Native.String;
using Jint.Runtime.Environments;
using Jint.Runtime.Interop;

namespace DirectPackageInstaller.Javascript;

public class Document : JsValue
{
    public object getElementById(string String)
    {
        var Node = HTML.DocumentNode.SelectSingleNode($"//*[@id='{String}']");

        if (Node == null)
            return false;

        ExpandoObject Object = new ExpandoObject();
        
        dynamic obj = Object;
        obj.Context = Node;

        var Dictionary = (IDictionary<String, Object>)Object;

        var StringAttributes = new string[] { "class" };
        foreach (var Attribute in Node.Attributes)
        {
            if (StringAttributes.Contains(Attribute.Name))
                Dictionary[Attribute.Name] = Attribute.Value;
            else
                Dictionary[Attribute.Name] = ConvertString(Attribute.Value);
        }

        Dictionary["getAttribute"] = new Func<string, object>(name =>
        {
            return Dictionary[name];
        });
    
        ((INotifyPropertyChanged)Object).PropertyChanged += OnPropertyChanged;
        
        return Object;
    }

    private object ConvertString(string Value)
    {
        if (long.TryParse(Value, out long lValue))
            return lValue;
        if (double.TryParse(Value, out double dValue))
            return Value;
        if (bool.TryParse(Value, out bool bValue))
            return bValue;
        return Value;
    }

    private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender == null)
            return;

        dynamic obj = sender;
        HtmlNode Node = obj.Context;

        var Dictionary = (IDictionary<String, Object>)obj;

        Node.SetAttributeValue(e.PropertyName, Dictionary[e.PropertyName].ToString());
    }

    public override object? ToObject()
    {
        return HTML.DocumentNode.ToString();
    }

    private Engine Context;
    private HtmlDocument HTML;

    public Document(Engine Engine, HtmlDocument Doc) : base(Engine.Global.Type)
    {
        this.HTML = Doc;
        this.Context = Engine;
    }
}