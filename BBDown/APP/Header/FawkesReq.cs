// <auto-generated>
//   This file was generated by a tool; you should avoid making direct changes.
//   Consider using 'partial classes' to extend these types
//   Input: fawkesreq.proto
// </auto-generated>

#region Designer generated code
#pragma warning disable CS0612, CS0618, CS1591, CS3021, IDE0079, IDE1006, RCS1036, RCS1057, RCS1085, RCS1192
[global::ProtoBuf.ProtoContract()]
public partial class FawkesReq : global::ProtoBuf.IExtensible
{
    private global::ProtoBuf.IExtension __pbn__extensionData;
    global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
        => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

    [global::ProtoBuf.ProtoMember(1, Name = @"appkey")]
    [global::System.ComponentModel.DefaultValue("")]
    public string Appkey
    {
        get => __pbn__Appkey ?? "";
        set => __pbn__Appkey = value;
    }
    public bool ShouldSerializeAppkey() => __pbn__Appkey != null;
    public void ResetAppkey() => __pbn__Appkey = null;
    private string __pbn__Appkey;

    [global::ProtoBuf.ProtoMember(2, Name = @"env")]
    [global::System.ComponentModel.DefaultValue("")]
    public string Env
    {
        get => __pbn__Env ?? "";
        set => __pbn__Env = value;
    }
    public bool ShouldSerializeEnv() => __pbn__Env != null;
    public void ResetEnv() => __pbn__Env = null;
    private string __pbn__Env;

    [global::ProtoBuf.ProtoMember(3)]
    [global::System.ComponentModel.DefaultValue("")]
    public string sessionId
    {
        get => __pbn__sessionId ?? "";
        set => __pbn__sessionId = value;
    }
    public bool ShouldSerializesessionId() => __pbn__sessionId != null;
    public void ResetsessionId() => __pbn__sessionId = null;
    private string __pbn__sessionId;

}

#pragma warning restore CS0612, CS0618, CS1591, CS3021, IDE0079, IDE1006, RCS1036, RCS1057, RCS1085, RCS1192
#endregion
