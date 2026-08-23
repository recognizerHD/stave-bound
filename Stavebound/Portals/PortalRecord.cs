using System;
using UnityEngine;

namespace Stavebound.Portals
{
    /// <summary>
    /// One portal, as everything outside <see cref="PortalRegistry"/> sees it.
    /// <para>
    /// It carries two identities on purpose. <see cref="Pid"/> is ours and permanent, and is what
    /// anything stored or sent refers to. <see cref="Id"/> is the game's ZDOID, which is how you
    /// actually reach the object — and which the game renumbers on every world load, so it is only
    /// ever valid for the session it arrived in (DESIGN.md §12).
    /// </para>
    /// <para>
    /// A record also carries the portal's <see cref="ClearanceMask"/>, because a client standing at
    /// portal A needs the mask of A's <em>target</em> — normally kilometres away and not in that
    /// client's ZDO set at all. The ZDO mirror alone cannot answer that, which is what makes this
    /// field the thing the travel gate and the inventory overlay both rest on. See DESIGN.md §6.
    /// </para>
    /// </summary>
    internal readonly struct PortalRecord : IEquatable<PortalRecord>
    {
        internal PortalRecord(long pid, ZDOID id, string name, Vector3 position, long targetPid, int clearanceMask)
        {
            Pid = pid;
            Id = id;
            Name = name ?? string.Empty;
            Position = position;
            TargetPid = targetPid;
            ClearanceMask = clearanceMask;
        }

        /// <summary>Our permanent id for this portal. Stable across relogs, unlike <see cref="Id"/>.</summary>
        internal long Pid { get; }

        /// <summary>
        /// The live ZDOID, for reaching the object this session. Never store it and never send it
        /// anywhere that outlives the session.
        /// </summary>
        internal ZDOID Id { get; }

        /// <summary>The portal's tag — what the player named it. May be empty.</summary>
        internal string Name { get; }

        /// <summary>World position, used to sort by distance and to place the map marker.</summary>
        internal Vector3 Position { get; }

        /// <summary>
        /// The <see cref="Pid"/> this portal sends you to, or <see cref="PortalTarget.NoPid"/> if it
        /// has never been re-aimed and is still following vanilla tag pairing. Pointers are one-way:
        /// that the target points back here is a coincidence, not an invariant (DESIGN.md §5).
        /// </summary>
        internal long TargetPid { get; }

        /// <summary>
        /// Per-tier clearance flags granted at this portal's site. Always zero until Phase 2 builds
        /// anchors and staves.
        /// </summary>
        internal int ClearanceMask { get; }

        internal void WriteTo(ZPackage package)
        {
            package.Write(Pid);
            package.Write(Id);
            package.Write(Name);
            package.Write(Position);
            package.Write(TargetPid);
            package.Write(ClearanceMask);
        }

        internal static PortalRecord ReadFrom(ZPackage package)
        {
            long pid = package.ReadLong();
            ZDOID id = package.ReadZDOID();
            string name = package.ReadString();
            Vector3 position = package.ReadVector3();
            long targetPid = package.ReadLong();
            int clearanceMask = package.ReadInt();
            return new PortalRecord(pid, id, name, position, targetPid, clearanceMask);
        }

        /// <summary>
        /// Value equality, used by the server sweep to decide whether anything actually changed and
        /// a broadcast is worth sending.
        /// <para>
        /// Position compares with Unity's approximate <c>==</c> on purpose. A portal does not move,
        /// but its position round-trips through float storage, and an exact comparison would let
        /// last-bit noise report a change every sweep and push the whole list to every client
        /// forever.
        /// </para>
        /// </summary>
        public bool Equals(PortalRecord other)
        {
            return Pid == other.Pid
                   && Id == other.Id
                   && TargetPid == other.TargetPid
                   && ClearanceMask == other.ClearanceMask
                   && string.Equals(Name, other.Name, StringComparison.Ordinal)
                   && Position == other.Position;
        }

        public override bool Equals(object obj) => obj is PortalRecord other && Equals(other);

        public override int GetHashCode() => Pid.GetHashCode();

        public override string ToString()
        {
            return string.IsNullOrEmpty(Name) ? "(unnamed portal)" : $"\"{Name}\"";
        }
    }
}
