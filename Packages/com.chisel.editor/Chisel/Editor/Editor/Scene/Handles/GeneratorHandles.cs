using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Chisel.Core;
using Chisel.Components;
using UnitySceneExtensions;
using UnityEditor.ShortcutManagement;

namespace Chisel.Editors
{
    public enum GeneratorModeState
    {
        None,

        Commit,
        Cancel,
        
        Update
    }

    [Flags]
    public enum ChiselGeneratorModeFlags
    {
        None                    = 0,
        
        SameLengthXZ            = 1,
        HeightEqualsMinXZ       = SameLengthXZ | 2,
        HeightEqualsHalfMinXZ   = SameLengthXZ | 4,

        // TODO: Generate position => Pivot
        GenerateFromCenterXZ    = 8,
        GenerateFromCenterY     = 16,
        
        AlwaysFaceUp            = 32,
        AlwaysFaceCameraXZ      = 64,
        
        UseLastHeight           = 128,
    }

    public abstract partial class ChiselGeneratorModeWithSettings<SettingsType, DefinitionType, Generator> : ChiselGeneratorMode
        where SettingsType          : ScriptableObject
        where DefinitionType    : IChiselGenerator, new()
        where Generator         : ChiselDefinedGeneratorComponent<DefinitionType>
    {
        public virtual ChiselGeneratorModeFlags Flags {  get { return ChiselGeneratorModeFlags.None; } }
        protected virtual void OnCreate(Generator generator) { }
        protected virtual void OnUpdate(Generator generator, Bounds bounds) { }
        protected virtual void OnPaint(Matrix4x4 transformation, Bounds bounds) { }


        Vector3 componentPosition   = Vector3.zero;
        Vector3 upAxis              = Vector3.zero;

        protected void DoBoxGenerationHandle(Rect dragArea, string nodeName)
        {
            var flags = Flags;            
            if (Event.current.shift)
                flags |= ChiselGeneratorModeFlags.UseLastHeight;

            switch (RectangleExtrusionHandle.Do(dragArea, out Bounds bounds, out float height, out ChiselModel modelBeneathCursor, out Matrix4x4 transformation, flags, Axis.Y))
            {
                case GeneratorModeState.Update:
                {
                    if (!generatedComponent)
                    {
                        if (height != 0)
                        {
                            generatedComponent = ChiselComponentFactory.Create<Generator>(nodeName,
                                                                        ChiselModelManager.GetActiveModelOrCreate(modelBeneathCursor),
                                                                        transformation);
                            componentPosition   = generatedComponent.transform.localPosition;
                            upAxis              = generatedComponent.transform.up;

                            generatedComponent.definition.Reset();
                            generatedComponent.Operation = forceOperation ?? CSGOperationType.Additive;
                            OnCreate(generatedComponent);
                            OnUpdate(generatedComponent, bounds);

                            if ((flags & ChiselGeneratorModeFlags.GenerateFromCenterY) == ChiselGeneratorModeFlags.GenerateFromCenterY)
                                generatedComponent.transform.localPosition = componentPosition - ((upAxis * height) * 0.5f);
                            generatedComponent.UpdateGenerator();
                        }
                    } else
                    {
                        ChiselComponentFactory.SetTransform(generatedComponent, transformation);
                        generatedComponent.Operation = forceOperation ??
                                                ((height < 0 && modelBeneathCursor) ?
                                                CSGOperationType.Subtractive :
                                                CSGOperationType.Additive);
                        OnUpdate(generatedComponent, bounds);
                        if ((flags & ChiselGeneratorModeFlags.GenerateFromCenterY) == ChiselGeneratorModeFlags.GenerateFromCenterY)
                            generatedComponent.transform.localPosition = componentPosition - ((upAxis * height) * 0.5f);
                    }
                    break;
                }
                
                case GeneratorModeState.Commit:     { if (generatedComponent) Commit(generatedComponent.gameObject); else Cancel(); break; }
                case GeneratorModeState.Cancel:     { Cancel(); break; }
            }

            if (ChiselOutlineRenderer.VisualizationMode != VisualizationMode.SimpleOutline)
                ChiselOutlineRenderer.VisualizationMode = VisualizationMode.SimpleOutline;
            OnPaint(transformation, bounds);
        }
    }
}
