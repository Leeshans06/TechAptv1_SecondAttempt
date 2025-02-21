// Copyright © 2025 Always Active Technologies PTY Ltd
using ProtoBuf;
namespace TechAptV1.Client.Models;

[ProtoContract]
public class Number
{
    [ProtoMember(1)]
    public int Value { get; set; }
    [ProtoMember(2)]
    public int IsPrime { get; set; }
}
