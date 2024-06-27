using UnityEditor;
using UnityEngine;

namespace Chisel.Editors
{
    public interface ISingletonData
    {
        void OnAfterDeserialize();
        void OnBeforeSerialize();
    }

    // TODO: Move all singletons over to this ..
    public class SingletonManager<DataType, SingletonInstanceType> : ScriptableObject, ISerializationCallbackReceiver
        where DataType				: ISingletonData, new()
        where SingletonInstanceType : SingletonManager<DataType,SingletonInstanceType>
    {
        #region Instance
        static SingletonManager<DataType, SingletonInstanceType> _instance;
        public static SingletonInstanceType Instance
        {
            get
            {
                if (_instance)
                    return _instance as SingletonInstanceType;
                
                _instance = ScriptableObject.CreateInstance<SingletonInstanceType>();
                _instance.hideFlags = HideFlags.HideAndDontSave;
                return _instance as SingletonInstanceType;  
            }
        }
        #endregion
        

        public DataType data = new DataType();

        public static DataType Data { get { return Instance.data; } }


        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            // This helps survive domain reloads
            if (_instance == null) _instance = this;
            var instance = _instance ? _instance : this;
            instance.data.OnAfterDeserialize();
        }

        void ISerializationCallbackReceiver.OnBeforeSerialize()
        {
            // This helps survive domain reloads
            if (_instance == null) _instance = this;
            var instance = _instance ? _instance : this;
            instance.data.OnBeforeSerialize();
        }

        void OnEnable()
        {
            // This helps survive domain reloads
            if (_instance == null) _instance = this;
            Initialize();
        }

        protected virtual void Initialize() { }

        void OnDestroy()
        {
            // This helps survive domain reloads
            if (_instance == this) _instance = null;
            Shutdown();
        }

        protected virtual void Shutdown() { }

        protected static void RecordUndo(string name)
        {
            Undo.RecordObject(Instance, name);
        }
    }

}
