// * * * * * * * * * * * * * * * * * * * * * * * * * * * * * *
// Author:             Daniel Cornelius (NukeAndBeans)
// Contact:            Twitter @nukeandbeans, Discord Nuke#3681
// License:
// Date/Time:          12-16-2021 @ 9:58 PM
// 
// Description:
// 
// * * * * * * * * * * * * * * * * * * * * * * * * * * * * * *


using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEngine;


namespace Chisel.Editors.MaterialBrowser
{
    [StructLayout( LayoutKind.Sequential )]
    internal abstract class BrowserTab<T> where T : Object
    {
        /// <summary>The title of this tile, shown on the tab in the UI</summary>
        public virtual string Title { get; }

        /// <summary>The content handled by this tab</summary>
        public virtual List<T> Content { get; }

        /// <summary>The filters used by this tile to find assets. See <see cref="SetSearchFilter"/></summary>
        public virtual List<string> SearchFilter { get; }

        /// <summary>The filters used by this tab to ignore specific assets.</summary>
        public virtual List<string> ExclusionFilter { get; }

        protected string UnitySearchFilter { get; set; }

        /// <summary>Convenience readonly property to get the number of tiles handled by this tab</summary>
        public virtual int Count
        {
            get => Content.Count;
        }

        /// <summary></summary>
        /// <param name="title">The title of this tab, shown in the UI</param>
        /// <param name="defaultSearchFilter">
        /// The default filter to apply to this tab. This is used by <see cref="GetAssets"/>
        /// to filter for the relevant assets
        /// <remarks>
        /// By default, browser tabs ignore the following directories:
        /// <para>
        /// "Packages/com.unity.searcher/"
        /// "Packages/com.unity.entities/"
        /// "Editor Resources"
        /// </para>
        /// </remarks>
        /// </param>
        public BrowserTab( string title, string defaultSearchFilter = "" )
        {
            Title   = title;
            Content = new List<T>();

            SearchFilter = new List<string>()
            {
                defaultSearchFilter
            };

            ExclusionFilter = new List<string>()
            {
                "packages/com.unity.searcher/",
                "packages/com.unity.entities/",
                "editor resources"
            };
        }

        /// <summary>Sets the filter used to find assets with <see cref="GetAssets"/></summary>
        /// <remarks>
        /// <see cref="SearchFilter"/> is cleared first before filters are added. 
        /// If you want to only APPEND a filter, then see <see cref="AppendSearchFilter"/>.
        /// </remarks>
        /// <param name="filter">The filters applied to this tab</param>
        public virtual void SetSearchFilter( params string[] filter )
        {
            SearchFilter.Clear();
            SearchFilter.AddRange( filter );
        }

        /// <summary>Adds a filter to <see cref="SearchFilter"/></summary>
        /// <param name="filter">The filters to add to this tab</param>
        public virtual void AppendSearchFilter( params string[] filter )
        {
            SearchFilter.AddRange( filter );
        }

        /// <summary>Sets the filter used to exclude specific assets when <see cref="GetAssets"/> is used.</summary>
        /// <remarks>
        /// <see cref="ExclusionFilter"/> is cleared first before filters are added. 
        /// If you want to only APPEND a filter, then see <see cref="AppendExclusionFilter"/>.
        /// </remarks>
        /// <param name="filter">The exclusion filters applied to this tab</param>
        public virtual void SetExclusionFilter( params string[] filter )
        {
            ExclusionFilter.Clear();
            ExclusionFilter.AddRange( filter );
        }

        /// <summary>Adds a filter to <see cref="ExclusionFilter"/></summary>
        /// <param name="filter">The exclusion filters to add to this tab</param>
        public virtual void AppendExclusionFilter( params string[] filter )
        {
            ExclusionFilter.AddRange( filter );
        }

        /// <summary>Reloads all content for this tab</summary>
        public virtual void Reload()
        {
            Content.Clear();
            Content.TrimExcess();

            GetAssets();
        }

        /// <summary>Gets the relevant assets for this tab.</summary>
        /// <remarks>This is defined for each tab, so it is up to each tab to have its own implementation.</remarks>
        protected abstract void GetAssets();

        /// <summary>Draw the content for this tab</summary>
        /// <remarks>This is defined for each tab, so it is up to each tab to have its own implementation.</remarks>
        /// <param name="drawArea">The overall area to draw the content of this tab.</param>
        /// <param name="tileSize">The size of each item to draw.</param>
        /// <param name="scrollContentRect">The scroll area rect containing this tab's content.</param>
        /// <param name="scrollPosition">The scroll position of the scroll area containing this tab's content.</param>
        public abstract void DrawContent( Rect drawArea, Vector2 tileSize, Rect scrollContentRect, Vector2 scrollPosition );
    }
}
