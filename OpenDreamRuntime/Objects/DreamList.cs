using System.Linq;
using OpenDreamRuntime.Objects.MetaObjects;
using OpenDreamRuntime.Procs;
using OpenDreamShared.Dream;
using Robust.Shared.Serialization.Manager;

namespace OpenDreamRuntime.Objects {
    delegate void DreamListValueAssignedEventHandler(DreamList list, DreamValue key, DreamValue value);
    delegate void DreamListBeforeValueRemovedEventHandler(DreamList list, DreamValue key, DreamValue value);

    [Virtual]
    public class DreamList : DreamObject {
        private static DreamObjectDefinition? _listDef;

        internal event DreamListValueAssignedEventHandler? ValueAssigned;
        internal event DreamListBeforeValueRemovedEventHandler? BeforeValueRemoved;

        private List<DreamValue> _values;
        private Dictionary<DreamValue, DreamValue>? _associativeValues;

        public virtual bool IsAssociative => (_associativeValues != null && _associativeValues.Count > 0);

        protected DreamList(int size = 0) : base(null) {
            _values = new List<DreamValue>(size);
            ObjectDefinition = _listDef ??= IoCManager.Resolve<IDreamManager>().ObjectTree.GetObjectDefinition(DreamPath.List);
        }

        public static DreamList CreateUninitialized(int size = 0) {
            return new DreamList(size);
        }

        public static DreamList Create(int size = 0) {
            return new DreamList(size);
        }

        public static DreamList Create(string[] collection) {
            var list = new DreamList(collection.Length);

            foreach (string value in collection) {
                list._values.Add(new DreamValue(value));
            }

            return list;
        }

        public DreamList CreateCopy(int start = 1, int end = 0) {
            if (start == 0) ++start; //start being 0 and start being 1 are equivalent
            if (end > _values.Count + 1) throw new Exception("list index out of bounds");
            if (end == 0) end = _values.Count + 1;

            DreamList copy = Create(end);

            for (int i = start; i < end; i++) {
                DreamValue value = _values[i - 1];

                copy._values.Add(value);
                if (ContainsKey(value)) {
                    copy.SetValue(value, _associativeValues[value]);
                }
            }

            return copy;
        }

        /// <summary>
        /// Returns the list of array values. Doesn't include the associative values indexable by some of these.
        /// </summary>
        public virtual List<DreamValue> GetValues() {
            return _values;
        }

        public Dictionary<DreamValue, DreamValue> GetAssociativeValues() {
            return _associativeValues ??= new Dictionary<DreamValue, DreamValue>();
        }

        public virtual DreamValue GetValue(DreamValue key) {
            if (key.TryGetValueAsInteger(out int keyInteger)) {
                return _values[keyInteger - 1]; //1-indexed
            }
            if (_associativeValues == null)
                return DreamValue.Null;

            return _associativeValues.TryGetValue(key, out DreamValue value) ? value : DreamValue.Null;
        }

        public virtual void SetValue(DreamValue key, DreamValue value, bool allowGrowth = false) {
            ValueAssigned?.Invoke(this, key, value);

            if (key.TryGetValueAsInteger(out int keyInteger)) {
                if (allowGrowth && keyInteger == _values.Count + 1) {
                    _values.Add(value);
                } else {
                    _values[keyInteger - 1] = value;
                }
            } else {
                if (!ContainsValue(key)) _values.Add(key);

                _associativeValues ??= new Dictionary<DreamValue, DreamValue>(1);
                _associativeValues[key] = value;
            }
        }

        public void RemoveValue(DreamValue value) {
            int valueIndex = _values.LastIndexOf(value);

            if (valueIndex != -1) {
                BeforeValueRemoved?.Invoke(this, new DreamValue(valueIndex), _values[valueIndex]);

                _values.RemoveAt(valueIndex);
            }
        }

        public virtual void AddValue(DreamValue value) {
            _values.Add(value);

            ValueAssigned?.Invoke(this, new DreamValue(_values.Count), value);
        }

        //Does not include associations
        public virtual bool ContainsValue(DreamValue value) {
            return _values.Contains(value);
        }

        public virtual bool ContainsKey(DreamValue value) {
            return _associativeValues != null && _associativeValues.ContainsKey(value);
        }

        public int FindValue(DreamValue value, int start = 1, int end = 0) {
            if (end == 0 || end > _values.Count) end = _values.Count;

            for (int i = start; i <= end; i++) {
                if (_values[i - 1].Equals(value)) return i;
            }

            return 0;
        }

        public virtual void Cut(int start = 1, int end = 0) {
            if (end == 0 || end > (_values.Count + 1)) end = _values.Count + 1;

            if (BeforeValueRemoved != null) {
                for (int i = end - 1; i >= start; i--) {
                    BeforeValueRemoved.Invoke(this, new DreamValue(i), _values[i - 1]);
                }
            }

            _values.RemoveRange(start - 1, end - start);
        }

        public void Insert(int index, DreamValue value) {
            _values.Insert(index - 1, value);
        }

        public void Swap(int index1, int index2) {
            DreamValue temp = GetValue(new DreamValue(index1));

            SetValue(new DreamValue(index1), GetValue(new DreamValue(index2)));
            SetValue(new DreamValue(index2), temp);
        }

        public void Resize(int size) {
            if (size > _values.Count) {
                _values.Capacity = size;

                for (int i = _values.Count; i < size; i++) {
                    AddValue(DreamValue.Null);
                }
            } else {
                Cut(size + 1);
            }
        }

        public virtual int GetLength() {
            return _values.Count;
        }

        public DreamList Union(DreamList other) {
            DreamList newList = new DreamList();
            newList._values = _values.Union(other.GetValues()).ToList();
            foreach ((DreamValue key, DreamValue value) in other.GetAssociativeValues()) {
                newList.SetValue(key, value);
            }

            return newList;
        }
    }

    // /datum.vars list
    sealed class DreamListVars : DreamList {
        private DreamObject _dreamObject;

        public override bool IsAssociative =>
            true; // We don't use the associative array but, yes, we behave like an associative list

        private DreamListVars(DreamObject dreamObject) : base() {
            _dreamObject = dreamObject;
        }

        public static DreamListVars Create(DreamObject dreamObject) {
            var list = new DreamListVars(dreamObject);
            list.InitSpawn(new DreamProcArguments(null));
            return list;
        }

        public override List<DreamValue> GetValues() {
            return _dreamObject.GetVariableNames();
        }

        public override bool ContainsKey(DreamValue value) {
            if (!value.TryGetValueAsString(out var varName)) {
                return false;
            }

            return _dreamObject.HasVariable(varName);
        }

        public override bool ContainsValue(DreamValue value) {
            return ContainsKey(value);
        }

        public override DreamValue GetValue(DreamValue key) {
            if (!key.TryGetValueAsString(out var varName)) {
                throw new Exception($"Invalid var index {key}");
            }

            if (!_dreamObject.TryGetVariable(varName, out var objectVar)) {
                throw new Exception(
                    $"Cannot get value of undefined var \"{key}\" on type {_dreamObject.ObjectDefinition.Type}");
            }

            return objectVar;
        }

        public override void SetValue(DreamValue key, DreamValue value, bool allowGrowth = false) {
            if (key.TryGetValueAsString(out var varName)) {
                if (!_dreamObject.HasVariable(varName)) {
                    throw new Exception(
                        $"Cannot set value of undefined var \"{varName}\" on type {_dreamObject.ObjectDefinition.Type}");
                }

                _dreamObject.SetVariable(varName, value);
            } else {
                throw new Exception($"Invalid var index {key}");
            }
        }
    }

    // global.vars list
    sealed class DreamGlobalVars : DreamList {
        [Dependency] private readonly IDreamManager _dreamMan = default!;

        public override bool IsAssociative =>
            true; // We don't use the associative array but, yes, we behave like an associative list

        private DreamGlobalVars() {
            IoCManager.InjectDependencies(this);
        }

        public static DreamGlobalVars Create() {
            var list = new DreamGlobalVars();
            return list;
        }

        public override List<DreamValue> GetValues() {
            var root = _dreamMan.ObjectTree.GetObjectDefinition(DreamPath.Root);
            List<DreamValue> values = new List<DreamValue>(root.GlobalVariables.Keys.Count - 1);
            // Skip world
            foreach (var key in root.GlobalVariables.Keys.Skip(1)) {
                values.Add(new DreamValue(key));
            }

            return values;
        }

        public override bool ContainsKey(DreamValue value) {
            if (!value.TryGetValueAsString(out var varName)) {
                return false;
            }

            return _dreamMan.ObjectTree.GetObjectDefinition(DreamPath.Root).GlobalVariables.ContainsKey(varName);
        }

        public override bool ContainsValue(DreamValue value) {
            return ContainsKey(value);
        }

        public override DreamValue GetValue(DreamValue key) {
            if (!key.TryGetValueAsString(out var varName)) {
                throw new Exception($"Invalid var index {key}");
            }

            var root = _dreamMan.ObjectTree.GetObjectDefinition(DreamPath.Root);
            if (!root.GlobalVariables.TryGetValue(varName, out var globalId)) {
                throw new Exception($"Invalid global {varName}");
            }

            return _dreamMan.Globals[globalId];
        }

        public override void SetValue(DreamValue key, DreamValue value, bool allowGrowth = false) {
            if (key.TryGetValueAsString(out var varName)) {
                var root = _dreamMan.ObjectTree.GetObjectDefinition(DreamPath.Root);
                if (!root.GlobalVariables.TryGetValue(varName, out var globalId)) {
                    throw new Exception($"Cannot set value of undefined global \"{varName}\"");
                }

                _dreamMan.Globals[globalId] = value;
            } else {
                throw new Exception($"Invalid var index {key}");
            }
        }
    }

    // atom.filters list
    // Operates on an atom's appearance
    public sealed class DreamFilterList : DreamList {
        private readonly IDreamManager _dreamManager;
        private readonly IAtomManager _atomManager;
        private readonly ISerializationManager _serializationManager;

        private readonly DreamObject _atom;

        public DreamFilterList(DreamObject atom) {
            _dreamManager = IoCManager.Resolve<IDreamManager>();
            _atomManager = IoCManager.Resolve<IAtomManager>();
            _serializationManager = IoCManager.Resolve<ISerializationManager>();
            _atom = atom;
        }

        public override void Cut(int start = 1, int end = 0) {
            _atomManager.UpdateAppearance(_atom, appearance => {
                int filterCount = appearance.Filters.Count + 1;
                if (end == 0 || end > filterCount) end = filterCount;

                appearance.Filters.RemoveRange(start - 1, end - start);
            });
        }

        public int GetIndexOfFilter(DreamFilter filter) {
            IconAppearance appearance = GetAppearance();

            return appearance.Filters.IndexOf(filter) + 1;
        }

        public void SetFilter(int index, DreamFilter filter) {
            IconAppearance appearance = GetAppearance();
            if (index < 1 || index > appearance.Filters.Count)
                throw new Exception($"Cannot index {index} on filter list");


            _atomManager.UpdateAppearance(_atom, appearance => {
                DreamFilter oldFilter = appearance.Filters[index - 1];

                DreamMetaObjectFilter.FilterAttachedTo.Remove(oldFilter);
                appearance.Filters[index - 1] = filter;
                DreamMetaObjectFilter.FilterAttachedTo[filter] = this;
            });
        }

        public override DreamValue GetValue(DreamValue key) {
            if (!key.TryGetValueAsInteger(out var filterIndex) || filterIndex < 1)
                throw new Exception($"Invalid index into filter list: {key}");

            IconAppearance appearance = GetAppearance();
            if (filterIndex > appearance.Filters.Count)
                throw new Exception($"Atom only has {appearance.Filters.Count} filter(s), cannot index {filterIndex}");

            DreamFilter filter = appearance.Filters[filterIndex - 1];
            DreamObject filterObject = _dreamManager.ObjectTree.CreateObject(DreamPath.Filter);
            DreamMetaObjectFilter.DreamObjectToFilter[filterObject] = filter;
            return new DreamValue(filterObject);
        }

        public override void SetValue(DreamValue key, DreamValue value, bool allowGrowth = false) {
            if (!value.TryGetValueAsDreamObjectOfType(DreamPath.Filter, out var filterObject))
                throw new Exception($"Cannot set value of filter list to {value}");
            if (!key.TryGetValueAsInteger(out var filterIndex) || filterIndex < 1)
                throw new Exception($"Invalid index into filter list: {key}");

            DreamFilter filter = DreamMetaObjectFilter.DreamObjectToFilter[filterObject];
            SetFilter(filterIndex, filter);
        }

        public override void AddValue(DreamValue value) {
            if (!value.TryGetValueAsDreamObjectOfType(DreamPath.Filter, out var filterObject))
                throw new Exception($"Cannot add {value} to filter list");

            DreamFilter filter = DreamMetaObjectFilter.DreamObjectToFilter[filterObject];
            DreamFilter copy = _serializationManager.Copy(filter); // Adding a filter creates a copy

            DreamMetaObjectFilter.FilterAttachedTo[copy] = this;
            _atomManager.UpdateAppearance(_atom, appearance => {
                appearance.Filters.Add(copy);
            });
        }

        public override int GetLength() {
            return GetAppearance().Filters.Count;
        }

        private IconAppearance GetAppearance() {
            IconAppearance? appearance = _atomManager.GetAppearance(_atom);
            if (appearance == null)
                throw new Exception("Atom has no appearance");

            return appearance;
        }
    }
}
