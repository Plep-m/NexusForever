﻿using System;
using System.Collections.Generic;
using System.Linq;
using NexusForever.Database.World.Model;
using NexusForever.Shared.GameTable;
using NexusForever.Shared.GameTable.Model;
using NexusForever.WorldServer.Game.Entity.Static;
using NexusForever.WorldServer.Game.Spell;
using NexusForever.WorldServer.Game.Spell.Static;
using NexusForever.WorldServer.Game.Static;

namespace NexusForever.WorldServer.Game.Entity
{
    public abstract class UnitEntity : WorldEntity
    {
        private readonly List<Spell.Spell> pendingSpells = new();

        public float HitRadius { get; protected set; } = 1f;

        protected UnitEntity(EntityType type)
            : base(type)
        {
            InitialiseHitRadius();
        }

        public override void Initialise(EntityModel model)
        {
            base.Initialise(model);
        }

        private void InitialiseHitRadius()
        {
            if (CreatureId == 0u)
                return;

            Creature2Entry creatureEntry = GameTableManager.Instance.Creature2.GetEntry(CreatureId);
            if (creatureEntry == null)
                return;

            Creature2ModelInfoEntry modelInfoEntry = GameTableManager.Instance.Creature2ModelInfo.GetEntry(creatureEntry.Creature2ModelInfoId);
            if (modelInfoEntry != null)
                HitRadius = modelInfoEntry.HitRadius * creatureEntry.ModelScale;
        }

        public override void Update(double lastTick)
        {
            base.Update(lastTick);

            foreach (Spell.Spell spell in pendingSpells.ToArray())
            {
                spell.Update(lastTick);
                spell.LateUpdate(lastTick);
                if (spell.IsFinished)
                    pendingSpells.Remove(spell);
            }
        }

        /// <summary>
        /// Cast a <see cref="Spell"/> with the supplied spell id and <see cref="SpellParameters"/>.
        /// </summary>
        public void CastSpell(uint spell4Id, SpellParameters parameters)
        {
            if (parameters == null)
                throw new ArgumentNullException();

            Spell4Entry spell4Entry = GameTableManager.Instance.Spell4.GetEntry(spell4Id);
            if (spell4Entry == null)
                throw new ArgumentOutOfRangeException();

            CastSpell(spell4Entry.Spell4BaseIdBaseSpell, (byte)spell4Entry.TierIndex, parameters);
        }

        /// <summary>
        /// Cast a <see cref="Spell"/> with the supplied spell base id, tier and <see cref="SpellParameters"/>.
        /// </summary>
        public void CastSpell(uint spell4BaseId, byte tier, SpellParameters parameters)
        {
            if (parameters == null)
                throw new ArgumentNullException();

            SpellBaseInfo spellBaseInfo = GlobalSpellManager.Instance.GetSpellBaseInfo(spell4BaseId);
            if (spellBaseInfo == null)
                throw new ArgumentOutOfRangeException();

            SpellInfo spellInfo = spellBaseInfo.GetSpellInfo(tier);
            if (spellInfo == null)
                throw new ArgumentOutOfRangeException();

            parameters.SpellInfo = spellInfo;
            CastSpell(parameters);
        }

        /// <summary>
        /// Cast a <see cref="Spell"/> with the supplied <see cref="SpellParameters"/>.
        /// </summary>
        public void CastSpell(SpellParameters parameters)
        {
            if (parameters == null)
                throw new ArgumentNullException();

            if (DisableManager.Instance.IsDisabled(DisableType.BaseSpell, parameters.SpellInfo.BaseInfo.Entry.Id))
            {
                if (this is Player player)
                    player.SendSystemMessage($"Unable to cast base spell {parameters.SpellInfo.BaseInfo.Entry.Id} because it is disabled.");
                return;
            }

            if (DisableManager.Instance.IsDisabled(DisableType.Spell, parameters.SpellInfo.Entry.Id))
            {
                if (this is Player player)
                    player.SendSystemMessage($"Unable to cast spell {parameters.SpellInfo.Entry.Id} because it is disabled.");
                return;
            }

            if (parameters.UserInitiatedSpellCast)
            {
                if (this is Player player)
                    player.Dismount();
            }

            CastMethod castMethod = (CastMethod)parameters.SpellInfo.BaseInfo.Entry.CastMethod;
            if (parameters.ClientSideInteraction != null)
                castMethod = CastMethod.ClientSideInteraction;

            var spell = GlobalSpellManager.Instance.NewSpell(castMethod, this, parameters);
            if (!spell.Cast())
                return;

            // Don't store spell if it failed to initialise
            if (spell.IsFailed)
                return;

            pendingSpells.Add(spell);
        }

        /// <summary>
        /// Cancel any <see cref="Spell"/>'s that are interrupted by movement.
        /// </summary>
        public void CancelSpellsOnMove()
        {
            foreach (Spell.Spell spell in pendingSpells)
                if (spell.IsMovingInterrupted() && spell.IsCasting)
                    spell.CancelCast(CastResult.CasterMovement);
        }

        /// <summary>
        /// Cancel a <see cref="Spell"/> based on its casting id
        /// </summary>
        /// <param name="castingId">Casting ID of the spell to cancel</param>
        public void CancelSpellCast(uint castingId)
        {
            Spell.Spell spell = pendingSpells.SingleOrDefault(s => s.CastingId == castingId);
            spell?.CancelCast(CastResult.SpellCancelled);
        }

        /// <summary>
        /// Checks if this <see cref="UnitEntity"/> is currently casting a spell.
        /// </summary>
        /// <returns></returns>
        public bool IsCasting()
        {
            foreach (Spell.Spell spell in pendingSpells)
                if (spell.IsCasting)
                    return true;

            return false;
        }

        /// <summary>
        /// Check if this <see cref="UnitEntity"/> has a spell active with the provided <see cref="Spell4Entry"/> Id
        /// </summary>
        public bool HasSpell(uint spell4Id, out Spell.Spell spell, bool isCasting = false)
        {
            spell = pendingSpells.FirstOrDefault(i => i.IsCasting == isCasting && !i.IsFinished && i.Spell4Id == spell4Id);

            return spell != null;
        }

        /// <summary>
        /// Check if this <see cref="UnitEntity"/> has a spell active with the provided <see cref="CastMethod"/>
        /// </summary>
        public bool HasSpell(CastMethod castMethod, out Spell.Spell spell)
        {
            spell = pendingSpells.FirstOrDefault(i => !i.IsCasting && !i.IsFinished && i.CastMethod == castMethod);

            return spell != null;
        }

        /// <summary>
        /// Check if this <see cref="UnitEntity"/> has a spell active with the provided <see cref="Func"/> predicate.
        /// </summary>
        public bool HasSpell(Func<Spell.Spell, bool> predicate, out Spell.Spell spell)
        {
            spell = pendingSpells.FirstOrDefault(predicate);

            return spell != null;
        }
    }
}
