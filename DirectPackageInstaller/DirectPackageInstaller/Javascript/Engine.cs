using System;
using System.Reflection;
using Jint;
using Jint.Native.Object;
using Jint.Runtime.Environments;
using Jint.Runtime.Interop;

namespace DirectPackageInstaller.Javascript;

public static class JSEngine
{
    public static Engine GetEngine(HtmlAgilityPack.HtmlDocument Context)
    {
        Engine JS = new Engine();
        JS.SetValue("Engine", new ObjectWrapper(JS, JS));
        JS.SetValue("HTML", new ObjectWrapper(JS, Context));
        JS.SetValue("DocumentClass", TypeReference.CreateTypeReference(JS, typeof(Document)));
        JS.Execute("this.document = new DocumentClass(Engine, HTML);");
        return JS;
    }
}