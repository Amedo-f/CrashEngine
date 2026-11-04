using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Twinsanity.AgentLab.AgentLabObjectDescs;
using Twinsanity.AgentLab.AgentLabObjectDescs.PS2;
using Twinsanity.AgentLab.Resolvers;
using Twinsanity.AgentLab.Resolvers.Interfaces;
using Twinsanity.Libraries;
using Twinsanity.TwinsanityInterchange.Common.AgentLab;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code.AgentLab;

namespace Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab
{
    public class PS2BehaviourStateBody : ITwinBehaviourStateBody
    {
        public UInt32 Bitfield { get; set; }
        public Boolean HasStateJump { get; set; }
        public Int32 JumpToState { get; set; }
        public bool Unknown { get; set; }
        public TwinBehaviourCondition Condition { get; set; }
        public List<ITwinBehaviourCommand> Commands { get; set; }

        bool ITwinBehaviourStateBody.HasNext { get; set; }

        public PS2BehaviourStateBody()
        {
            Commands = new List<ITwinBehaviourCommand>();
        }

        public int GetLength()
        {
            return 4 + (HasStateJump ? 4 : 0) + (Commands?.Sum(command => command.GetLength()) ?? 0) + (Condition?.GetLength() ?? 0);
        }

        public void Compile()
        {
            return;
        }

        public void Decompile(IResolver resolver, StreamWriter writer, int tabs = 0)
        {
            if (Condition == null)
            {
                StringUtils.WriteLineTabulated(writer, "if Else(0) >= 0.5 {", tabs);
                StringUtils.WriteLineTabulated(writer, "interval = 1.0;", tabs + 1);
                StringUtils.WriteLineTabulated(writer, "unknown = false;", tabs + 1);
            }
            else
            {
                Condition.Decompile(resolver, writer, tabs);
            }
            StringUtils.WriteLineTabulated(writer, $"unknown = {Unknown.ToString().ToLower()};", tabs + 1);

            tabs += 1;
            foreach (var cmd in Commands)
            {
                cmd.Decompile(resolver, writer, tabs);
            }

            if (HasStateJump)
            {
                StringUtils.WriteLineTabulated(writer, $"execute State_{JumpToState};", tabs);
            }

            tabs -= 1;
            StringUtils.WriteLineTabulated(writer, "}", tabs);
        }

        public void Read(BinaryReader reader, int length)
        {
            Bitfield = reader.ReadUInt32();
            var hasStateJump = (Bitfield & 0x400) != 0;
            var hasCondition = (Bitfield & 0x200) != 0;
            var hasTraceEnabled = (Bitfield & 0x100) != 0; // Unused in retail
            var commandsAmt = Bitfield & 0xFF;
            HasStateJump = hasStateJump;
            Unknown = hasTraceEnabled;
            if (HasStateJump)
            {
                JumpToState = reader.ReadInt32();
            }
            if (hasCondition)
            {
                Condition = new TwinBehaviourCondition();
                Condition.Read(reader, length);
            }
            Commands.Clear();
            for (var i = 0; i < commandsAmt; ++i)
            {
                var com = new PS2BehaviourCommand();
                Commands.Add(com);
                com.Read(reader, length);
            }
        }

        public void Write(BinaryWriter writer)
        {
            UInt32 newBitfield = (UInt32)Commands.Count;
            ITwinBehaviourStateBody downCast = this;
            if (downCast.HasNext)
            {
                newBitfield |= 0x800;
            }
            if (HasStateJump)
            {
                newBitfield |= 0x400;
            }
            if (Condition != null)
            {
                newBitfield |= 0x200;
            }
            if (Unknown)
            {
                newBitfield |= 0x100;
            }
            
            Bitfield = newBitfield;
            writer.Write(newBitfield);
            if (HasStateJump)
            {
                writer.Write(JumpToState);
            }
            Condition?.Write(writer);
            foreach (var com in Commands)
            {
                com.HasNext = !Commands.Last().Equals(com);
                com.Write(writer);
            };
        }
        public void WriteText(StreamWriter writer, Int32 tabs = 0)
        {
            StringUtils.WriteLineTabulated(writer, "Body {", tabs);
 
            if (HasStateJump)
            {
                StringUtils.WriteLineTabulated(writer, $"next_state = {JumpToState}", tabs + 1);
            }
            // Amedo 2026-11-04: bit 0x100 of the body (Unknown) = after this body runs, the state machine re-checks the state
            // straight away (AgentLabAgent.StateMachine RunBody -> ctx.Reenter). The text never carried it, so editing a script in
            // the editor dropped it from 184 scripts (spin, death, knee drop...). Written only when set, so other scripts' text
            // is unchanged. If a behaviour edited in the editor loops or stalls differently, check this line first.
            if (Unknown)
            {
                StringUtils.WriteLineTabulated(writer, "reenter = true", tabs + 1);
            }
            Condition?.WriteText(writer, tabs + 1);
            foreach (var cmd in Commands)
            {
                cmd.WriteText(writer, tabs + 1);
            }
            StringUtils.WriteLineTabulated(writer, "}", tabs);
        }

        public void ReadText(StreamReader reader)
        {
            String line = "";
            Condition = null;
            Commands.Clear();
            Unknown = false; // Amedo 2026-11-04: the "reenter" line below sets it back (see WriteText)
            while (!line.EndsWith("}"))
            {
                line = reader.ReadLine().Trim();
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }
                if (line.StartsWith("next_state "))
                {
                    JumpToState = int.Parse(StringUtils.GetStringAfter(line, "="));
                    HasStateJump = true;
                }
                else if (line.StartsWith("reenter")) // Amedo 2026-11-04
                {
                    Unknown = Boolean.Parse(StringUtils.GetStringAfter(line, "=").Trim().TrimEnd(';').Trim());
                }
                else if (line.StartsWith("Condition"))
                {
                    Condition = new TwinBehaviourCondition();
                    String condName = StringUtils.GetStringInBetween(line, "Condition ", "(").Trim();
                    Condition.Parameter = ushort.Parse(StringUtils.GetStringInBetween(line, "(", ")"));
                    while (!line.EndsWith("{"))
                    {
                        line = reader.ReadLine().Trim();
                    }
                    Condition.ReadText(reader, condName);
                }
                else if (!line.StartsWith("}"))
                {
                    PS2BehaviourCommand cmd = new();
                    cmd.ReadText(line);
                    Commands.Add(cmd);
                }
            }
            // Amedo 2026-11-04: the interpreter reads Bitfield straight away (Play right after Apply, before any save), and only
            // Write used to rebuild it, so it stayed 0 here. Same bits as Write; 0x800 (another body follows) is set by Write.
            Bitfield = (UInt32)Commands.Count | (Bitfield & 0x800) | (HasStateJump ? 0x400u : 0u) | (Condition != null ? 0x200u : 0u) | (Unknown ? 0x100u : 0u);
        }
    }
}
