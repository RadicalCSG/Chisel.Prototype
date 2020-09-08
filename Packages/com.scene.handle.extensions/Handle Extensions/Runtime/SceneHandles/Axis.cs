using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace UnitySceneExtensions
{
    public enum Axis 
    {
        None = -1,
        X = 0,
        Y = 1,
        Z = 2
    }

    [Flags]
    public enum Axes
    {
        None = 0,
        X	= 1 << Axis.X,
        Y	= 1 << Axis.Y,
        Z	= 1 << Axis.Z,
        XY	= X | Y,
        XZ	= X | Z,
        YZ	= Y | Z,
        XYZ	= X | Y | Z
    }
    
    public enum PlaneAxes
    {
        XZ = Axes.X | Axes.Z,
        XY = Axes.X | Axes.Y,
        YZ = Axes.Y | Axes.Z
    };
}
