using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Interface;
using ImGuiNET;
using OtterGui;
using OtterGui.Raii;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;
using Penumbra.Interop.Structs;
using Penumbra.Meta.Files;
using Penumbra.Meta.Manipulations;
using Penumbra.Mods;
using Penumbra.Util;

namespace Penumbra.UI.Classes;

public partial class ModEditWindow
{
    private void DrawMetaTab()
    {
        using var tab = ImRaii.TabItem( "Meta Manipulations" );
        if( !tab )
        {
            return;
        }

        DrawOptionSelectHeader();

        var setsEqual = !_editor!.Meta.Changes;
        var tt        = setsEqual ? "No changes staged." : "Apply the currently staged changes to the option.";
        ImGui.NewLine();
        if( ImGuiUtil.DrawDisabledButton( "Apply Changes", Vector2.Zero, tt, setsEqual ) )
        {
            _editor.ApplyManipulations();
        }

        ImGui.SameLine();
        tt = setsEqual ? "No changes staged." : "Revert all currently staged changes.";
        if( ImGuiUtil.DrawDisabledButton( "Revert Changes", Vector2.Zero, tt, setsEqual ) )
        {
            _editor.RevertManipulations();
        }

        using var child = ImRaii.Child( "##meta", -Vector2.One, true );
        if( !child )
        {
            return;
        }

        DrawEditHeader( _editor.Meta.Eqp, "Equipment Parameter Edits (EQP)###EQP", 4, EqpRow.Draw, EqpRow.DrawNew );
        DrawEditHeader( _editor.Meta.Eqdp, "Racial Model Edits (EQDP)###EQDP", 6, EqdpRow.Draw, EqdpRow.DrawNew );
        DrawEditHeader( _editor.Meta.Imc, "Variant Edits (IMC)###IMC", 8, ImcRow.Draw, ImcRow.DrawNew );
        DrawEditHeader( _editor.Meta.Est, "Extra Skeleton Parameters (EST)###EST", 6, EstRow.Draw, EstRow.DrawNew );
        DrawEditHeader( _editor.Meta.Gmp, "Visor/Gimmick Edits (GMP)###GMP", 6, GmpRow.Draw, GmpRow.DrawNew );
        DrawEditHeader( _editor.Meta.Rsp, "Racial Scaling Edits (RSP)###RSP", 4, RspRow.Draw, RspRow.DrawNew );
    }


    // The headers for the different meta changes all have basically the same structure for different types.
    private void DrawEditHeader< T >( IReadOnlyCollection< T > items, string label, int numColumns, Action< T, Mod.Editor, Vector2 > draw,
        Action< Mod.Editor, Vector2 > drawNew )
    {
        const ImGuiTableFlags flags = ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.BordersInnerV;
        if( !ImGui.CollapsingHeader( $"{items.Count} {label}" ) )
        {
            return;
        }

        using( var table = ImRaii.Table( label, numColumns, flags ) )
        {
            if( table )
            {
                drawNew( _editor!, _iconSize );
                foreach( var (item, index) in items.ToArray().WithIndex() )
                {
                    using var id = ImRaii.PushId( index );
                    draw( item, _editor!, _iconSize );
                }
            }
        }

        ImGui.NewLine();
    }

    private static class EqpRow
    {
        private static EqpManipulation _new = new(Eqp.DefaultEntry, EquipSlot.Head, 1);

        private static float IdWidth
            => 100 * ImGuiHelpers.GlobalScale;

        public static void DrawNew( Mod.Editor editor, Vector2 iconSize )
        {
            ImGui.TableNextColumn();
            var canAdd       = editor.Meta.CanAdd( _new );
            var tt           = canAdd ? "Stage this edit." : "This entry is already edited.";
            var defaultEntry = ExpandedEqpFile.GetDefault( _new.SetId );
            if( ImGuiUtil.DrawDisabledButton( FontAwesomeIcon.Plus.ToIconString(), iconSize, tt, !canAdd, true ) )
            {
                editor.Meta.Add( _new with { Entry = defaultEntry } );
            }

            // Identifier
            ImGui.TableNextColumn();
            if( IdInput( "##eqpId", IdWidth, _new.SetId, out var setId, ExpandedEqpGmpBase.Count - 1 ) )
            {
                _new = _new with { SetId = setId };
            }

            ImGui.TableNextColumn();
            if( EqpEquipSlotCombo( "##eqpSlot", _new.Slot, out var slot ) )
            {
                _new = _new with { Slot = slot };
            }

            // Values
            ImGui.TableNextColumn();
            using var style = ImRaii.PushStyle( ImGuiStyleVar.ItemSpacing, ImGui.GetStyle().ItemSpacing / 2 );
            foreach( var flag in Eqp.EqpAttributes[ _new.Slot ] )
            {
                using var id    = ImRaii.PushId( ( int )flag );
                var       value = defaultEntry.HasFlag( flag );
                Checkmark( string.Empty, flag.ToLocalName(), value, value, out _ );
                ImGui.SameLine();
            }
        }

        public static void Draw( EqpManipulation meta, Mod.Editor editor, Vector2 iconSize )
        {
            ImGui.TableNextColumn();
            if( ImGuiUtil.DrawDisabledButton( FontAwesomeIcon.Trash.ToIconString(), iconSize, "Delete this meta edit.", false, true ) )
            {
                editor.Meta.Delete( meta );
            }

            // Identifier
            ImGui.TableNextColumn();
            ImGui.SetCursorPosX( ImGui.GetCursorPosX() + ImGui.GetStyle().FramePadding.X );
            ImGui.TextUnformatted( meta.SetId.ToString() );
            var defaultEntry = ExpandedEqpFile.GetDefault( meta.SetId );
            ImGui.TableNextColumn();
            ImGui.SetCursorPosX( ImGui.GetCursorPosX() + ImGui.GetStyle().FramePadding.X );
            ImGui.TextUnformatted( meta.Slot.ToName() );

            // Values
            ImGui.TableNextColumn();
            using var style = ImRaii.PushStyle( ImGuiStyleVar.ItemSpacing, ImGui.GetStyle().ItemSpacing / 2 );
            foreach( var flag in Eqp.EqpAttributes[ meta.Slot ] )
            {
                using var id           = ImRaii.PushId( ( int )flag );
                var       defaultValue = defaultEntry.HasFlag( flag );
                var       currentValue = meta.Entry.HasFlag( flag );
                if( Checkmark( string.Empty, flag.ToLocalName(), currentValue, defaultValue, out var value ) )
                {
                    editor.Meta.Change( meta with { Entry = value ? meta.Entry | flag : meta.Entry & ~flag } );
                }

                ImGui.SameLine();
            }
        }
    }


    private static class EqdpRow
    {
        private static EqdpManipulation _new = new(EqdpEntry.Invalid, EquipSlot.Head, Gender.Male, ModelRace.Midlander, 1);

        private static float IdWidth
            => 100 * ImGuiHelpers.GlobalScale;

        public static void DrawNew( Mod.Editor editor, Vector2 iconSize )
        {
            ImGui.TableNextColumn();
            var raceCode      = Names.CombinedRace( _new.Gender, _new.Race );
            var validRaceCode = CharacterUtility.EqdpIdx( raceCode, false ) >= 0;
            var canAdd        = validRaceCode && editor.Meta.CanAdd( _new );
            var tt = canAdd   ? "Stage this edit." :
                validRaceCode ? "This entry is already edited." : "This combination of race and gender can not be used.";
            var defaultEntry = validRaceCode
                ? ExpandedEqdpFile.GetDefault( Names.CombinedRace( _new.Gender, _new.Race ), _new.Slot.IsAccessory(), _new.SetId )
                : 0;
            if( ImGuiUtil.DrawDisabledButton( FontAwesomeIcon.Plus.ToIconString(), iconSize, tt, !canAdd, true ) )
            {
                editor.Meta.Add( _new with { Entry = defaultEntry } );
            }

            // Identifier
            ImGui.TableNextColumn();
            if( IdInput( "##eqdpId", IdWidth, _new.SetId, out var setId, ExpandedEqpGmpBase.Count - 1 ) )
            {
                _new = _new with { SetId = setId };
            }

            ImGui.TableNextColumn();
            if( RaceCombo( "##eqdpRace", _new.Race, out var race ) )
            {
                _new = _new with { Race = race };
            }

            ImGui.TableNextColumn();
            if( GenderCombo( "##eqdpGender", _new.Gender, out var gender ) )
            {
                _new = _new with { Gender = gender };
            }

            ImGui.TableNextColumn();
            if( EqdpEquipSlotCombo( "##eqdpSlot", _new.Slot, out var slot ) )
            {
                _new = _new with { Slot = slot };
            }

            // Values
            ImGui.TableNextColumn();
            var (bit1, bit2) = defaultEntry.ToBits( _new.Slot );
            Checkmark( "##eqdpCheck1", string.Empty, bit1, bit1, out _ );
            ImGui.SameLine();
            Checkmark( "##eqdpCheck2", string.Empty, bit2, bit2, out _ );
        }

        public static void Draw( EqdpManipulation meta, Mod.Editor editor, Vector2 iconSize )
        {
            ImGui.TableNextColumn();
            if( ImGuiUtil.DrawDisabledButton( FontAwesomeIcon.Trash.ToIconString(), iconSize, "Delete this meta edit.", false, true ) )
            {
                editor.Meta.Delete( meta );
            }

            // Identifier
            ImGui.TableNextColumn();
            ImGui.SetCursorPosX( ImGui.GetCursorPosX() + ImGui.GetStyle().FramePadding.X );
            ImGui.TextUnformatted( meta.SetId.ToString() );
            ImGui.TableNextColumn();
            ImGui.SetCursorPosX( ImGui.GetCursorPosX() + ImGui.GetStyle().FramePadding.X );
            ImGui.TextUnformatted( meta.Race.ToName() );
            ImGui.TableNextColumn();
            ImGui.SetCursorPosX( ImGui.GetCursorPosX() + ImGui.GetStyle().FramePadding.X );
            ImGui.TextUnformatted( meta.Gender.ToName() );
            ImGui.TableNextColumn();
            ImGui.SetCursorPosX( ImGui.GetCursorPosX() + ImGui.GetStyle().FramePadding.X );
            ImGui.TextUnformatted( meta.Slot.ToName() );

            // Values
            var defaultEntry = ExpandedEqdpFile.GetDefault( Names.CombinedRace( meta.Gender, meta.Race ), meta.Slot.IsAccessory(), meta.SetId );
            var (defaultBit1, defaultBit2) = defaultEntry.ToBits( meta.Slot );
            var (bit1, bit2)               = meta.Entry.ToBits( meta.Slot );
            ImGui.TableNextColumn();
            if( Checkmark( "##eqdpCheck1", string.Empty, bit1, defaultBit1, out var newBit1 ) )
            {
                editor.Meta.Change( meta with { Entry = Eqdp.FromSlotAndBits( meta.Slot, newBit1, bit2 ) } );
            }

            ImGui.SameLine();
            if( Checkmark( "##eqdpCheck2", string.Empty, bit2, defaultBit2, out var newBit2 ) )
            {
                editor.Meta.Change( meta with { Entry = Eqdp.FromSlotAndBits( meta.Slot, bit1, newBit2 ) } );
            }
        }
    }

    private static class ImcRow
    {
        private static ImcManipulation _new = new(EquipSlot.Head, 1, 1, new ImcEntry());

        private static float IdWidth
            => 80 * ImGuiHelpers.GlobalScale;

        private static float SmallIdWidth
            => 45 * ImGuiHelpers.GlobalScale;

        // Convert throwing to null-return if the file does not exist.
        private static ImcEntry? GetDefault( ImcManipulation imc )
        {
            try
            {
                return ImcFile.GetDefault( imc.GamePath(), imc.EquipSlot, imc.Variant );
            }
            catch
            {
                return null;
            }
        }

        public static void DrawNew( Mod.Editor editor, Vector2 iconSize )
        {
            ImGui.TableNextColumn();
            var defaultEntry = GetDefault( _new );
            var canAdd = defaultEntry != null && editor.Meta.CanAdd( _new );
            var tt = canAdd ? "Stage this edit." : defaultEntry == null ? "This IMC file does not exist." : "This entry is already edited.";
            defaultEntry ??= new ImcEntry();
            if( ImGuiUtil.DrawDisabledButton( FontAwesomeIcon.Plus.ToIconString(), iconSize, tt, !canAdd, true ) )
            {
                editor.Meta.Add( _new with { Entry = defaultEntry.Value } );
            }

            // Identifier
            ImGui.TableNextColumn();
            if( ImcTypeCombo( "##imcType", _new.ObjectType, out var type ) )
            {
                _new = new ImcManipulation( type, _new.BodySlot, _new.PrimaryId, _new.SecondaryId == 0 ? ( ushort )1 : _new.SecondaryId,
                    _new.Variant, _new.EquipSlot == EquipSlot.Unknown ? EquipSlot.Head : _new.EquipSlot, _new.Entry );
            }

            ImGui.TableNextColumn();
            if( IdInput( "##imcId", IdWidth, _new.PrimaryId, out var setId, ushort.MaxValue ) )
            {
                _new = _new with { PrimaryId = setId };
            }

            ImGui.TableNextColumn();
            // Equipment and accessories are slightly different imcs than other types.
            if( _new.ObjectType is ObjectType.Equipment or ObjectType.Accessory )
            {
                if( EqdpEquipSlotCombo( "##imcSlot", _new.EquipSlot, out var slot ) )
                {
                    _new = _new with { EquipSlot = slot };
                }
            }
            else
            {
                if( IdInput( "##imcId2", 100 * ImGuiHelpers.GlobalScale, _new.SecondaryId, out var setId2, ushort.MaxValue ) )
                {
                    _new = _new with { SecondaryId = setId2 };
                }
            }

            ImGui.TableNextColumn();
            if( IdInput( "##imcVariant", SmallIdWidth, _new.Variant, out var variant, byte.MaxValue ) )
            {
                _new = _new with { Variant = variant };
            }

            // Values
            ImGui.TableNextColumn();
            IntDragInput( "##imcMaterialId", "Material ID", SmallIdWidth, defaultEntry.Value.MaterialId, defaultEntry.Value.MaterialId, out _,
                1, byte.MaxValue, 0f );
            ImGui.SameLine();
            IntDragInput( "##imcMaterialAnimId", "Material Animation ID", SmallIdWidth, defaultEntry.Value.MaterialAnimationId,
                defaultEntry.Value.MaterialAnimationId, out _, 0, byte.MaxValue, 0.01f );
            ImGui.TableNextColumn();
            IntDragInput( "##imcDecalId", "Decal ID", SmallIdWidth, defaultEntry.Value.DecalId, defaultEntry.Value.DecalId, out _, 0,
                byte.MaxValue, 0f );
            ImGui.SameLine();
            IntDragInput( "##imcVfxId", "VFX ID", SmallIdWidth, defaultEntry.Value.VfxId, defaultEntry.Value.VfxId, out _, 0, byte.MaxValue,
                0f );
            ImGui.SameLine();
            IntDragInput( "##imcSoundId", "Sound ID", SmallIdWidth, defaultEntry.Value.SoundId, defaultEntry.Value.SoundId, out _, 0, 0b111111,
                0f );
            ImGui.TableNextColumn();
            IntDragInput( "##imcAttributes", "Attributes", IdWidth, defaultEntry.Value.AttributeMask, defaultEntry.Value.AttributeMask, out _,
                0, 0b1111111111, 0f );
        }

        public static void Draw( ImcManipulation meta, Mod.Editor editor, Vector2 iconSize )
        {
            ImGui.TableNextColumn();
            if( ImGuiUtil.DrawDisabledButton( FontAwesomeIcon.Trash.ToIconString(), iconSize, "Delete this meta edit.", false, true ) )
            {
                editor.Meta.Delete( meta );
            }

            // Identifier
            ImGui.TableNextColumn();
            ImGui.SetCursorPosX( ImGui.GetCursorPosX() + ImGui.GetStyle().FramePadding.X );
            ImGui.TextUnformatted( meta.ObjectType.ToName() );
            ImGui.TableNextColumn();
            ImGui.SetCursorPosX( ImGui.GetCursorPosX() + ImGui.GetStyle().FramePadding.X );
            ImGui.TextUnformatted( meta.PrimaryId.ToString() );

            ImGui.TableNextColumn();
            ImGui.SetCursorPosX( ImGui.GetCursorPosX() + ImGui.GetStyle().FramePadding.X );
            if( meta.ObjectType is ObjectType.Equipment or ObjectType.Accessory )
            {
                ImGui.TextUnformatted( meta.EquipSlot.ToName() );
            }
            else
            {
                ImGui.TextUnformatted( meta.SecondaryId.ToString() );
            }

            ImGui.TableNextColumn();
            ImGui.SetCursorPosX( ImGui.GetCursorPosX() + ImGui.GetStyle().FramePadding.X );
            ImGui.TextUnformatted( meta.Variant.ToString() );

            // Values
            ImGui.TableNextColumn();
            var defaultEntry = GetDefault( meta ) ?? new ImcEntry();
            if( IntDragInput( "##imcMaterialId", $"Material ID\nDefault Value: {defaultEntry.MaterialId}", SmallIdWidth, meta.Entry.MaterialId,
                   defaultEntry.MaterialId, out var materialId, 1, byte.MaxValue, 0.01f ) )
            {
                editor.Meta.Change( meta with { Entry = meta.Entry with { MaterialId = ( byte )materialId } } );
            }

            ImGui.SameLine();
            if( IntDragInput( "##imcMaterialAnimId", $"Material Animation ID\nDefault Value: {defaultEntry.MaterialAnimationId}", SmallIdWidth,
                   meta.Entry.MaterialAnimationId, defaultEntry.MaterialAnimationId, out var materialAnimId, 0, byte.MaxValue, 0.01f ) )
            {
                editor.Meta.Change( meta with { Entry = meta.Entry with { MaterialAnimationId = ( byte )materialAnimId } } );
            }

            ImGui.TableNextColumn();
            if( IntDragInput( "##imcDecalId", $"Decal ID\nDefault Value: {defaultEntry.DecalId}", SmallIdWidth, meta.Entry.DecalId,
                   defaultEntry.DecalId, out var decalId, 0, byte.MaxValue, 0.01f ) )
            {
                editor.Meta.Change( meta with { Entry = meta.Entry with { DecalId = ( byte )decalId } } );
            }

            ImGui.SameLine();
            if( IntDragInput( "##imcVfxId", $"VFX ID\nDefault Value: {defaultEntry.VfxId}", SmallIdWidth, meta.Entry.VfxId, defaultEntry.VfxId,
                   out var vfxId, 0, byte.MaxValue, 0.01f ) )
            {
                editor.Meta.Change( meta with { Entry = meta.Entry with { VfxId = ( byte )vfxId } } );
            }

            ImGui.SameLine();
            if( IntDragInput( "##imcSoundId", $"Sound ID\nDefault Value: {defaultEntry.SoundId}", SmallIdWidth, meta.Entry.SoundId,
                   defaultEntry.SoundId, out var soundId, 0, 0b111111, 0.01f ) )
            {
                editor.Meta.Change( meta with { Entry = meta.Entry with { SoundId = ( byte )soundId } } );
            }

            ImGui.TableNextColumn();
            if( IntDragInput( "##imcAttributes", $"Attributes\nDefault Value: {defaultEntry.AttributeMask}", IdWidth,
                   meta.Entry.AttributeMask, defaultEntry.AttributeMask, out var attributeMask, 0, 0b1111111111, 0.1f ) )
            {
                editor.Meta.Change( meta with { Entry = meta.Entry with { AttributeMask = ( ushort )attributeMask } } );
            }
        }
    }

    private static class EstRow
    {
        private static EstManipulation _new = new(Gender.Male, ModelRace.Midlander, EstManipulation.EstType.Body, 1, 0);

        private static float IdWidth
            => 100 * ImGuiHelpers.GlobalScale;

        public static void DrawNew( Mod.Editor editor, Vector2 iconSize )
        {
            ImGui.TableNextColumn();
            var canAdd       = editor.Meta.CanAdd( _new );
            var tt           = canAdd ? "Stage this edit." : "This entry is already edited.";
            var defaultEntry = EstFile.GetDefault( _new.Slot, Names.CombinedRace( _new.Gender, _new.Race ), _new.SetId );
            if( ImGuiUtil.DrawDisabledButton( FontAwesomeIcon.Plus.ToIconString(), iconSize, tt, !canAdd, true ) )
            {
                editor.Meta.Add( _new with { Entry = defaultEntry } );
            }

            // Identifier
            ImGui.TableNextColumn();
            if( IdInput( "##estId", IdWidth, _new.SetId, out var setId, ExpandedEqpGmpBase.Count - 1 ) )
            {
                _new = _new with { SetId = setId };
            }

            ImGui.TableNextColumn();
            if( RaceCombo( "##estRace", _new.Race, out var race ) )
            {
                _new = _new with { Race = race };
            }

            ImGui.TableNextColumn();
            if( GenderCombo( "##estGender", _new.Gender, out var gender ) )
            {
                _new = _new with { Gender = gender };
            }

            ImGui.TableNextColumn();
            if( EstSlotCombo( "##estSlot", _new.Slot, out var slot ) )
            {
                _new = _new with { Slot = slot };
            }

            // Values
            ImGui.TableNextColumn();
            IntDragInput( "##estSkeleton", "Skeleton Index", IdWidth, _new.Entry, defaultEntry, out _, 0, ushort.MaxValue, 0.05f );
        }

        public static void Draw( EstManipulation meta, Mod.Editor editor, Vector2 iconSize )
        {
            ImGui.TableNextColumn();
            if( ImGuiUtil.DrawDisabledButton( FontAwesomeIcon.Trash.ToIconString(), iconSize, "Delete this meta edit.", false, true ) )
            {
                editor.Meta.Delete( meta );
            }

            // Identifier
            ImGui.TableNextColumn();
            ImGui.SetCursorPosX( ImGui.GetCursorPosX() + ImGui.GetStyle().FramePadding.X );
            ImGui.TextUnformatted( meta.SetId.ToString() );
            ImGui.TableNextColumn();
            ImGui.SetCursorPosX( ImGui.GetCursorPosX() + ImGui.GetStyle().FramePadding.X );
            ImGui.TextUnformatted( meta.Race.ToName() );
            ImGui.TableNextColumn();
            ImGui.SetCursorPosX( ImGui.GetCursorPosX() + ImGui.GetStyle().FramePadding.X );
            ImGui.TextUnformatted( meta.Gender.ToName() );
            ImGui.TableNextColumn();
            ImGui.SetCursorPosX( ImGui.GetCursorPosX() + ImGui.GetStyle().FramePadding.X );
            ImGui.TextUnformatted( meta.Slot.ToString() );

            // Values
            var defaultEntry = EstFile.GetDefault( meta.Slot, Names.CombinedRace( meta.Gender, meta.Race ), meta.SetId );
            ImGui.TableNextColumn();
            if( IntDragInput( "##estSkeleton", $"Skeleton Index\nDefault Value: {defaultEntry}", IdWidth, meta.Entry, defaultEntry,
                   out var entry, 0, ushort.MaxValue, 0.05f ) )
            {
                editor.Meta.Change( meta with { Entry = ( ushort )entry } );
            }
        }
    }

    private static class GmpRow
    {
        private static GmpManipulation _new = new(GmpEntry.Default, 1);

        private static float RotationWidth
            => 75 * ImGuiHelpers.GlobalScale;

        private static float UnkWidth
            => 50 * ImGuiHelpers.GlobalScale;

        private static float IdWidth
            => 100 * ImGuiHelpers.GlobalScale;

        public static void DrawNew( Mod.Editor editor, Vector2 iconSize )
        {
            ImGui.TableNextColumn();
            var canAdd       = editor.Meta.CanAdd( _new );
            var tt           = canAdd ? "Stage this edit." : "This entry is already edited.";
            var defaultEntry = ExpandedGmpFile.GetDefault( _new.SetId );
            if( ImGuiUtil.DrawDisabledButton( FontAwesomeIcon.Plus.ToIconString(), iconSize, tt, !canAdd, true ) )
            {
                editor.Meta.Add( _new with { Entry = defaultEntry } );
            }

            // Identifier
            ImGui.TableNextColumn();
            if( IdInput( "##gmpId", IdWidth, _new.SetId, out var setId, ExpandedEqpGmpBase.Count - 1 ) )
            {
                _new = _new with { SetId = setId };
            }

            // Values
            ImGui.TableNextColumn();
            Checkmark( "##gmpEnabled", "Gimmick Enabled", defaultEntry.Enabled, defaultEntry.Enabled, out _ );
            ImGui.TableNextColumn();
            Checkmark( "##gmpAnimated", "Gimmick Animated", defaultEntry.Animated, defaultEntry.Animated, out _ );
            ImGui.TableNextColumn();
            IntDragInput( "##gmpRotationA", "Rotation A in Degrees", RotationWidth, defaultEntry.RotationA, defaultEntry.RotationA, out _, 0,
                360, 0f );
            ImGui.SameLine();
            IntDragInput( "##gmpRotationB", "Rotation B in Degrees", RotationWidth, defaultEntry.RotationB, defaultEntry.RotationB, out _, 0,
                360, 0f );
            ImGui.SameLine();
            IntDragInput( "##gmpRotationC", "Rotation C in Degrees", RotationWidth, defaultEntry.RotationC, defaultEntry.RotationC, out _, 0,
                360, 0f );
            ImGui.TableNextColumn();
            IntDragInput( "##gmpUnkA", "Animation Type A?", UnkWidth, defaultEntry.UnknownA, defaultEntry.UnknownA, out _, 0, 15, 0f );
            ImGui.SameLine();
            IntDragInput( "##gmpUnkB", "Animation Type B?", UnkWidth, defaultEntry.UnknownB, defaultEntry.UnknownB, out _, 0, 15, 0f );
        }

        public static void Draw( GmpManipulation meta, Mod.Editor editor, Vector2 iconSize )
        {
            ImGui.TableNextColumn();
            if( ImGuiUtil.DrawDisabledButton( FontAwesomeIcon.Trash.ToIconString(), iconSize, "Delete this meta edit.", false, true ) )
            {
                editor.Meta.Delete( meta );
            }

            // Identifier
            ImGui.TableNextColumn();
            ImGui.SetCursorPosX( ImGui.GetCursorPosX() + ImGui.GetStyle().FramePadding.X );
            ImGui.TextUnformatted( meta.SetId.ToString() );

            // Values
            var defaultEntry = ExpandedGmpFile.GetDefault( meta.SetId );
            ImGui.TableNextColumn();
            if( Checkmark( "##gmpEnabled", "Gimmick Enabled", meta.Entry.Enabled, defaultEntry.Enabled, out var enabled ) )
            {
                editor.Meta.Change( meta with { Entry = meta.Entry with { Enabled = enabled } } );
            }

            ImGui.TableNextColumn();
            if( Checkmark( "##gmpAnimated", "Gimmick Animated", meta.Entry.Animated, defaultEntry.Animated, out var animated ) )
            {
                editor.Meta.Change( meta with { Entry = meta.Entry with { Animated = animated } } );
            }

            ImGui.TableNextColumn();
            if( IntDragInput( "##gmpRotationA", $"Rotation A in Degrees\nDefault Value: {defaultEntry.RotationA}", RotationWidth,
                   meta.Entry.RotationA, defaultEntry.RotationA, out var rotationA, 0, 360, 0.05f ) )
            {
                editor.Meta.Change( meta with { Entry = meta.Entry with { RotationA = ( ushort )rotationA } } );
            }

            ImGui.SameLine();
            if( IntDragInput( "##gmpRotationB", $"Rotation B in Degrees\nDefault Value: {defaultEntry.RotationB}", RotationWidth,
                   meta.Entry.RotationB, defaultEntry.RotationB, out var rotationB, 0, 360, 0.05f ) )
            {
                editor.Meta.Change( meta with { Entry = meta.Entry with { RotationB = ( ushort )rotationB } } );
            }

            ImGui.SameLine();
            if( IntDragInput( "##gmpRotationC", $"Rotation C in Degrees\nDefault Value: {defaultEntry.RotationC}", RotationWidth,
                   meta.Entry.RotationC, defaultEntry.RotationC, out var rotationC, 0, 360, 0.05f ) )
            {
                editor.Meta.Change( meta with { Entry = meta.Entry with { RotationC = ( ushort )rotationC } } );
            }

            ImGui.TableNextColumn();
            if( IntDragInput( "##gmpUnkA", $"Animation Type A?\nDefault Value: {defaultEntry.UnknownA}", UnkWidth, meta.Entry.UnknownA,
                   defaultEntry.UnknownA, out var unkA, 0, 15, 0.01f ) )
            {
                editor.Meta.Change( meta with { Entry = meta.Entry with { UnknownA = ( byte )unkA } } );
            }

            ImGui.SameLine();
            if( IntDragInput( "##gmpUnkB", $"Animation Type B?\nDefault Value: {defaultEntry.UnknownB}", UnkWidth, meta.Entry.UnknownB,
                   defaultEntry.UnknownB, out var unkB, 0, 15, 0.01f ) )
            {
                editor.Meta.Change( meta with { Entry = meta.Entry with { UnknownA = ( byte )unkB } } );
            }
        }
    }

    private static class RspRow
    {
        private static RspManipulation _new = new(SubRace.Midlander, RspAttribute.MaleMinSize, 1f);

        private static float FloatWidth
            => 150 * ImGuiHelpers.GlobalScale;

        public static void DrawNew( Mod.Editor editor, Vector2 iconSize )
        {
            ImGui.TableNextColumn();
            var canAdd       = editor.Meta.CanAdd( _new );
            var tt           = canAdd ? "Stage this edit." : "This entry is already edited.";
            var defaultEntry = CmpFile.GetDefault( _new.SubRace, _new.Attribute );
            if( ImGuiUtil.DrawDisabledButton( FontAwesomeIcon.Plus.ToIconString(), iconSize, tt, !canAdd, true ) )
            {
                editor.Meta.Add( _new with { Entry = defaultEntry } );
            }

            // Identifier
            ImGui.TableNextColumn();
            if( SubRaceCombo( "##rspSubRace", _new.SubRace, out var subRace ) )
            {
                _new = _new with { SubRace = subRace };
            }

            ImGui.TableNextColumn();
            if( RspAttributeCombo( "##rspAttribute", _new.Attribute, out var attribute ) )
            {
                _new = _new with { Attribute = attribute };
            }

            // Values
            ImGui.TableNextColumn();
            ImGui.SetNextItemWidth( FloatWidth );
            ImGui.DragFloat( "##rspValue", ref defaultEntry, 0f );
        }

        public static void Draw( RspManipulation meta, Mod.Editor editor, Vector2 iconSize )
        {
            ImGui.TableNextColumn();
            if( ImGuiUtil.DrawDisabledButton( FontAwesomeIcon.Trash.ToIconString(), iconSize, "Delete this meta edit.", false, true ) )
            {
                editor.Meta.Delete( meta );
            }

            // Identifier
            ImGui.TableNextColumn();
            ImGui.SetCursorPosX( ImGui.GetCursorPosX() + ImGui.GetStyle().FramePadding.X );
            ImGui.TextUnformatted( meta.SubRace.ToName() );
            ImGui.TableNextColumn();
            ImGui.SetCursorPosX( ImGui.GetCursorPosX() + ImGui.GetStyle().FramePadding.X );
            ImGui.TextUnformatted( meta.Attribute.ToFullString() );
            ImGui.TableNextColumn();

            // Values
            var def   = CmpFile.GetDefault( meta.SubRace, meta.Attribute );
            var value = meta.Entry;
            ImGui.SetNextItemWidth( FloatWidth );
            using var color = ImRaii.PushColor( ImGuiCol.FrameBg,
                def < value ? ColorId.IncreasedMetaValue.Value() : ColorId.DecreasedMetaValue.Value(),
                def != value );
            if( ImGui.DragFloat( "##rspValue", ref value, 0.001f, 0.01f, 8f ) && value is >= 0.01f and <= 8f )
            {
                editor.Meta.Change( meta with { Entry = value } );
            }

            ImGuiUtil.HoverTooltip( $"Default Value: {def:0.###}" );
        }
    }

    // Different combos to use with enums.
    private static bool RaceCombo( string label, ModelRace current, out ModelRace race )
        => ImGuiUtil.GenericEnumCombo( label, 100 * ImGuiHelpers.GlobalScale, current, out race, RaceEnumExtensions.ToName, 1 );

    private static bool GenderCombo( string label, Gender current, out Gender gender )
        => ImGuiUtil.GenericEnumCombo( label, 120 * ImGuiHelpers.GlobalScale, current, out gender, RaceEnumExtensions.ToName, 1 );

    private static bool EqdpEquipSlotCombo( string label, EquipSlot current, out EquipSlot slot )
        => ImGuiUtil.GenericEnumCombo( label, 100 * ImGuiHelpers.GlobalScale, current, out slot, EquipSlotExtensions.EqdpSlots,
            EquipSlotExtensions.ToName );

    private static bool EqpEquipSlotCombo( string label, EquipSlot current, out EquipSlot slot )
        => ImGuiUtil.GenericEnumCombo( label, 100 * ImGuiHelpers.GlobalScale, current, out slot, EquipSlotExtensions.EquipmentSlots,
            EquipSlotExtensions.ToName );

    private static bool SubRaceCombo( string label, SubRace current, out SubRace subRace )
        => ImGuiUtil.GenericEnumCombo( label, 150 * ImGuiHelpers.GlobalScale, current, out subRace, RaceEnumExtensions.ToName, 1 );

    private static bool RspAttributeCombo( string label, RspAttribute current, out RspAttribute attribute )
        => ImGuiUtil.GenericEnumCombo( label, 200 * ImGuiHelpers.GlobalScale, current, out attribute,
            RspAttributeExtensions.ToFullString, 0, 1 );

    private static bool EstSlotCombo( string label, EstManipulation.EstType current, out EstManipulation.EstType attribute )
        => ImGuiUtil.GenericEnumCombo( label, 200 * ImGuiHelpers.GlobalScale, current, out attribute );

    private static bool ImcTypeCombo( string label, ObjectType current, out ObjectType type )
        => ImGuiUtil.GenericEnumCombo( label, 110 * ImGuiHelpers.GlobalScale, current, out type, ObjectTypeExtensions.ValidImcTypes,
            ObjectTypeExtensions.ToName );

    // A number input for ids with a optional max id of given width.
    // Returns true if newId changed against currentId.
    private static bool IdInput( string label, float width, ushort currentId, out ushort newId, int maxId )
    {
        int tmp = currentId;
        ImGui.SetNextItemWidth( width );
        if( ImGui.InputInt( label, ref tmp, 0 ) )
        {
            tmp = Math.Clamp( tmp, 1, maxId );
        }

        newId = ( ushort )tmp;
        return newId != currentId;
    }

    // A checkmark that compares against a default value and shows a tooltip.
    // Returns true if newValue is changed against currentValue.
    private static bool Checkmark( string label, string tooltip, bool currentValue, bool defaultValue, out bool newValue )
    {
        using var color = ImRaii.PushColor( ImGuiCol.FrameBg,
            defaultValue ? ColorId.DecreasedMetaValue.Value() : ColorId.IncreasedMetaValue.Value(), defaultValue != currentValue );
        newValue = currentValue;
        ImGui.Checkbox( label, ref newValue );
        ImGuiUtil.HoverTooltip( tooltip );
        return newValue != currentValue;
    }

    // A dragging int input of given width that compares against a default value, shows a tooltip and clamps against min and max.
    // Returns true if newValue changed against currentValue.
    private static bool IntDragInput( string label, string tooltip, float width, int currentValue, int defaultValue, out int newValue,
        int minValue, int maxValue, float speed )
    {
        newValue = currentValue;
        using var color = ImRaii.PushColor( ImGuiCol.FrameBg,
            defaultValue > currentValue ? ColorId.DecreasedMetaValue.Value() : ColorId.IncreasedMetaValue.Value(),
            defaultValue != currentValue );
        ImGui.SetNextItemWidth( width );
        if( ImGui.DragInt( label, ref newValue, speed, minValue, maxValue ) )
        {
            newValue = Math.Clamp( newValue, minValue, maxValue );
        }

        ImGuiUtil.HoverTooltip( tooltip );

        return newValue != currentValue;
    }
}