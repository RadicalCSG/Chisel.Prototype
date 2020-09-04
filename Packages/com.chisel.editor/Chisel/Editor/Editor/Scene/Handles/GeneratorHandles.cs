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
        where SettingsType      : ScriptableObject
        where DefinitionType    : IChiselGenerator, new()
        where Generator         : ChiselDefinedGeneratorComponent<DefinitionType>
    {
        Vector3 componentPosition   = Vector3.zero;
        Vector3 upAxis              = Vector3.zero;

        protected IGeneratorHandleRenderer renderer = new GeneratorHandleRenderer();

        protected void DoGenerationHandle(Rect dragArea, IChiselShapeGeneratorSettings<DefinitionType> settings)
        {
            // TODO: handle snapping against own points
            // TODO: handle ability to 'commit' last point
            switch (ShapeExtrusionHandle.Do(dragArea, out Curve2D shape, out float height, out ChiselModel modelBeneathCursor, out Matrix4x4 transformation, Axis.Y))
            {
                case ShapeExtrusionState.Create:
                {
                    var center2D = shape.Center;
                    var center3D = new Vector3(center2D.x, 0, center2D.y);
                    generatedComponent = ChiselComponentFactory.Create<Generator>(ToolName,
                                                                          ChiselModelManager.GetActiveModelOrCreate(modelBeneathCursor), 
                                                                          transformation * Matrix4x4.TRS(center3D, Quaternion.identity, Vector3.one));
                    shape.Center = Vector2.zero;
                    generatedComponent.definition.Reset();
                    generatedComponent.Operation = forceOperation ?? CSGOperationType.Additive;
                    settings.OnCreate(ref generatedComponent.definition, shape);
                    generatedComponent.UpdateGenerator();
                    break;
                }

                case ShapeExtrusionState.Modified:
                {
                    generatedComponent.Operation = forceOperation ?? 
                                              ((height < 0 && modelBeneathCursor) ? 
                                                CSGOperationType.Subtractive : 
                                                CSGOperationType.Additive);
                    settings.OnUpdate(ref generatedComponent.definition, height);
                    generatedComponent.UpdateGenerator();
                    break;
                }
                
                
                case ShapeExtrusionState.Commit:        { Commit(generatedComponent.gameObject); break; }
                case ShapeExtrusionState.Cancel:        { Cancel(); break; }
            }

            if (ChiselOutlineRenderer.VisualizationMode != VisualizationMode.SimpleOutline)
                ChiselOutlineRenderer.VisualizationMode = VisualizationMode.SimpleOutline;

            renderer.matrix = transformation;
            settings.OnPaint(renderer, shape, height);
        }

        protected void DoGenerationHandle(Rect dragArea, IChiselBoundsGeneratorSettings<DefinitionType> settings)
        {
            var generatoreModeFlags = settings.GeneratoreModeFlags;            
            if (Event.current.shift)
                generatoreModeFlags |= ChiselGeneratorModeFlags.UseLastHeight;

            switch (RectangleExtrusionHandle.Do(dragArea, out Bounds bounds, out float height, out ChiselModel modelBeneathCursor, out Matrix4x4 transformation, generatoreModeFlags, Axis.Y))
            {
                case GeneratorModeState.Update:
                {
                    if (!generatedComponent)
                    {
                        if (height != 0)
                        {
                            generatedComponent = ChiselComponentFactory.Create<Generator>(ToolName,
                                                                        ChiselModelManager.GetActiveModelOrCreate(modelBeneathCursor),
                                                                        transformation);
                            componentPosition   = generatedComponent.transform.localPosition;
                            upAxis              = generatedComponent.transform.up;

                            generatedComponent.definition.Reset();
                            generatedComponent.Operation = forceOperation ?? CSGOperationType.Additive;
                            settings.OnCreate(ref generatedComponent.definition);
                            settings.OnUpdate(ref generatedComponent.definition, bounds);
                            generatedComponent.OnValidate();

                            if ((generatoreModeFlags & ChiselGeneratorModeFlags.GenerateFromCenterY) == ChiselGeneratorModeFlags.GenerateFromCenterY)
                                generatedComponent.transform.localPosition = componentPosition - ((upAxis * height) * 0.5f);
                            generatedComponent.UpdateGenerator();
                        }
                    } else
                    {
                        ChiselComponentFactory.SetTransform(generatedComponent, transformation);
                        if ((generatoreModeFlags & ChiselGeneratorModeFlags.AlwaysFaceUp) == ChiselGeneratorModeFlags.AlwaysFaceCameraXZ)
                            generatedComponent.Operation = forceOperation ?? CSGOperationType.Additive;
                        else
                            generatedComponent.Operation = forceOperation ??
                                                    ((height < 0 && modelBeneathCursor) ?
                                                    CSGOperationType.Subtractive :
                                                    CSGOperationType.Additive);
                        settings.OnUpdate(ref generatedComponent.definition, bounds);
                        generatedComponent.OnValidate();
                        if ((generatoreModeFlags & ChiselGeneratorModeFlags.GenerateFromCenterY) == ChiselGeneratorModeFlags.GenerateFromCenterY)
                            generatedComponent.transform.localPosition = componentPosition - ((upAxis * height) * 0.5f);
                    }
                    break;
                }
                
                case GeneratorModeState.Commit:     { if (generatedComponent) Commit(generatedComponent.gameObject); else Cancel(); break; }
                case GeneratorModeState.Cancel:     { Cancel(); break; }
            }

            if (ChiselOutlineRenderer.VisualizationMode != VisualizationMode.SimpleOutline)
                ChiselOutlineRenderer.VisualizationMode = VisualizationMode.SimpleOutline;
            renderer.matrix = transformation;
            settings.OnPaint(renderer, bounds);
        }
    }
}
