/* * * * * * * * * * * * * * * * * * * * * *
Chisel.Editors.ChiselUIElements.cs

License:
Author: Daniel Cornelius

* * * * * * * * * * * * * * * * * * * * * */

using System;
using System.Collections.Generic;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Chisel.Editors
{
    public static class ChiselUIElements
    {
        public static void SetRotation( this VisualElement v, int degrees )
        {
            if( v.transform.rotation != Quaternion.Euler( 0, 0, degrees ) )
                v.schedule.Execute( () => { v.transform.rotation = Quaternion.Euler( 0, 0, degrees ); } );
        }

        public static void SetPosition( this VisualElement v, Vector2 position )
        {
            if( v.transform.position != new Vector3( position[0], position[1], v.transform.position.z ) )
                v.schedule.Execute( () => { v.transform.position = new Vector3( position[0], position[1], v.transform.position.z ); } );
        }

        public static void SetSize( this VisualElement v, Vector2 size )
        {
            if( v.style.width != size[0] || v.style.height != size[1] )
            {
                v.schedule.Execute( () =>
                {
                    v.style.width  = size[0];
                    v.style.height = size[1];
                } );
            }
        }

        public static Box AddBox( this VisualElement v, Rect sizeAndPosition, Color tint = default, int depth = 0 )
        {
            if(tint == default) tint = new Color( 0.5f, 0.5f, 0.5f, 0.5f );

            Box box = new Box();
            box.style.width           = sizeAndPosition.width;
            box.style.height          = sizeAndPosition.height;
            box.style.backgroundColor = tint;

            v.Add( box );

            return box;
        }

#region BUTTON

        public static void AddButton( this VisualElement v, string label, Rect sizeAndPosition, int depth = 0, Action action = null )
        {
            if( action == null )
            {
                action = () => {}; // just add an empty action, this button is doing nothing yet
                Debug.LogWarning( $"The button with title \"{label}\" was created without any action, it will always do nothing." );
            }

            Button button = new Button( action );
            button.text         = label;
            button.style.width  = sizeAndPosition.width;
            button.style.height = sizeAndPosition.height;

            button.transform.position = new Vector3( sizeAndPosition.x, sizeAndPosition.y, depth );

            v.Add( button );
        }

        public static void AddButton( this Toolbar t, string label, Vector2Int size, int depth = 0, Action action = null )
        {
            if( action == null )
            {
                action = () => {}; // just add an empty action, this button is doing nothing yet
                Debug.LogWarning( $"The toolbar button with title \"{label}\" was created without any action, it will always do nothing." );
            }

            ToolbarButton button = new ToolbarButton( action );
            button.text         = label;
            button.style.width  = size[0];
            button.style.height = size[1];

            button.transform.position = new Vector3( 0, 0, depth );

            t.Add( button );
        }

#endregion BUTTON

        public static Toolbar AddToolbar( this VisualElement v, Rect sizeAndPosition, int depth = 0 )
        {
            Toolbar toolbar = new Toolbar();
            toolbar.style.width  = sizeAndPosition.width;
            toolbar.style.height = sizeAndPosition.height;

            toolbar.transform.position = new Vector3( sizeAndPosition.x, sizeAndPosition.y, depth );

            v.Add( toolbar );

            return toolbar;
        }
    }
}
