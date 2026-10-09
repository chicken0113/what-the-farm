using System;
using System.IO;
using UnityEditor;
using UnityEngine;

// One-shot, local editor requests. There is no network service or runtime code.
[InitializeOnLoad]
public static class UnityMigrationBridge
{
    const string Request = "Library/WhatTheFarmMigration.request";
    const string Result = "Library/WhatTheFarmMigration.result";
    static UnityMigrationBridge() => EditorApplication.update += Tick;
    static void Tick()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(Request)) return;
        string mode=File.ReadAllText(Request).Trim(); File.Delete(Request);
        try
        {
            if(mode=="install") { UnityMigrationBuilder.Install(); File.WriteAllText(Result,"INSTALL_SUCCESS"); }
            else if(mode=="playcheck") UnityMigrationPlayCheck.Run();
            else throw new InvalidOperationException("Unknown migration request");
        }
        catch(Exception error) { Debug.LogException(error); File.WriteAllText(Result,error.ToString()); }
    }
}
