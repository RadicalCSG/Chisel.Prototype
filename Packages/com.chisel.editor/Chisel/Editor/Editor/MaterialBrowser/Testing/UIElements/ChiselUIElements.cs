/* * * * * * * * * * * * * * * * * * * * * *
Chisel.Editors.ChiselUIElements.cs

License:
Author: Daniel Cornelius

* * * * * * * * * * * * * * * * * * * * * */

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Chisel.Editors
{
    public static class ChiselUIElements
    {
#region CONFIG

        public static void GetRootElement( this EditorWindow window, string uxmlPath, string ussPath = "EditorWindow" )
        {
            //VisualTreeAsset m_VTree = Resources.Load<VisualTreeAsset>( $"Editor/Chisel/{uxmlPath}" );

            //m_VTree.CloneTree( window.rootVisualElement );

            window.rootVisualElement.styleSheets.Add( Resources.Load<StyleSheet>( $"Editor/Chisel/{ussPath}" ) );
        }

        public static void LoadStyleSheet( this VisualElement element, string name )
        {
            element.styleSheets.Add( Resources.Load<StyleSheet>( $"Editor/Chisel/{name}" ) );
        }

        public static void SetRotation( this VisualElement v, int degrees )
        {
            if( v.transform.rotation != Quaternion.Euler( 0, 0, degrees ) )
                v.schedule.Execute( () => { v.transform.rotation = Quaternion.Euler( 0, 0, degrees ); } );
        }

        public static void SetPosition( this VisualElement v, float x, float y )
        {
            if( v.transform.position != new Vector3( x, y, v.transform.position.z ) )
                v.schedule.Execute( () => { v.transform.position = new Vector3( x, y, v.transform.position.z ); } );
        }

        public static void SetSize( this VisualElement v, float width, float height )
        {
            if( v.style.width != width || v.style.height != height )
            {
                v.schedule.Execute( () =>
                {
                    v.style.width  = width;
                    v.style.height = height;
                } );
            }
        }

        public static void SetText( this Label label, string text )
        {
            if( label.text != text )
                label.schedule.Execute( () => { label.text = text; } );
        }

        public static void SetText( this Button button, string text )
        {
            if( button.text != text )
                button.schedule.Execute( () => { button.text = text; } );
        }

#endregion CONFIG

#region BUTTON

        public static Button AddButton( this VisualElement v, string label, string elementName = "", Action action = null, bool canClick = true )
        {
            if( action == null )
            {
                action = () => {}; // just add an empty action, this button is doing nothing yet
                Debug.LogWarning( $"The button with title \"{label}\" was created without any action, it will always do nothing." );
            }

            Button button = new Button( action );
            button.text = label;
            button.name = elementName;
            button.SetEnabled( canClick );

            v.Add( button );

            return button;
        }

        public static Button AddButton( this Toolbar t, string label, string elementName = "", Action action = null, bool canClick = true )
        {
            if( action == null )
            {
                action = () => {}; // just add an empty action, this button is doing nothing yet
                Debug.LogWarning( $"The toolbar button with title \"{label}\" was created without any action, it will always do nothing." );
            }

            ToolbarButton button = new ToolbarButton( action );
            button.text = label;
            button.name = elementName;
            button.SetEnabled( canClick );

            t.Add( button );

            return button;
        }

#endregion BUTTON

        public static Toolbar AddToolbar( this VisualElement v, string elementName = "" )
        {
            Toolbar toolbar = new Toolbar();
            toolbar.name = elementName;

            v.Add( toolbar );

            return toolbar;
        }

        public static ToolbarSpacer AddSpacer( this Toolbar t, string elementName )
        {
            ToolbarSpacer spacer = new ToolbarSpacer();

            spacer.name = elementName;

            t.Add( spacer );

            return spacer;
        }

        public static Label AddLabel( this VisualElement v, string text, string elementName = "" )
        {
            Label label = new Label( text );
            label.name = elementName;

            v.Add( label );

            return label;
        }

        public static Box AddBox( this VisualElement v, string elementName = "" )
        {
            Box box = new Box();
            box.name = elementName;

            v.Add( box );

            return box;
        }

        public static ScrollView AddScrollView<T>( this VisualElement v, in List<T> elements, ref Vector2 scrollPos, in int tileSize, bool horizontalScrollBar = false, string elementName = "",
                                                   string             contentContainerName = "contentContainer" ) where T : VisualElement
        {
            ScrollView sv = new ScrollView( ScrollViewMode.Vertical );

            sv.name           = elementName;
            sv.scrollOffset   = scrollPos;
            sv.elasticity     = 0;
            sv.showHorizontal = horizontalScrollBar;

            sv.contentContainer.name = contentContainerName;

            foreach( var ve in elements )
            {
                ve.SetSize( tileSize, tileSize );
                sv.AddContent( ve );
            }

            v.Add( sv );

            return sv;
        }

        public static void AddContent( this ScrollView sv, VisualElement element )
        {
            sv.schedule.Execute( () => { sv.Add( element ); } );
        }

        public static Image AddImage( this VisualElement v, Texture2D image, string elementName = "" )
        {
            Image i = new Image();
            i.name      = elementName;
            i.image     = image;
            i.scaleMode = ScaleMode.ScaleToFit;
            i.uv        = new Rect( 0, 1, image.width, image.height );

            v.Add( i );

            return i;
        }

        public static Slider AddSlider( this VisualElement v, out int value, int min, int max, string label, string elementName = "" )
        {
            Slider slider = new Slider( min, max, SliderDirection.Horizontal );
            slider.name  = elementName;
            slider.label = label;

            value = (int) slider.value;

            v.Add( slider );

            return slider;
        }
    }
}
