using System.Collections.Generic;
using System.Linq;

namespace Chisel.Components
{
    public static class HashSetExtensions
    {
        public static bool Set<T>(this HashSet<T> self, HashSet<T> other)
        {
            var modified = !self.ContentsEquals(other);
            if (!modified)
                return false;
            self.Clear();
            foreach (var item in other)
                self.Add(item);
            return modified;
        }

        public static bool Set<T>(this HashSet<T> self, IEnumerable<T> other)
        {
            var modified = !self.ContentsEquals(other);
            if (!modified)
                return false;
            self.Clear();
            foreach (var item in other)
                self.Add(item);
            return modified;
        }


        public static bool SetCommon<T>(this HashSet<T> self, HashSet<T> A, HashSet<T> B)
        {
            var minCount = (A.Count >= B.Count) ? B.Count : A.Count;
            var modified = true;
            if (minCount == self.Count)
            {
                modified = false;
                foreach(var item in self)
                {
                    if (!A.Contains(item) ||
                        !B.Contains(item))
                    {
                        modified = true;
                        break;
                    }
                }
            }
            if (!modified)
                return false;
            self.Clear();
            foreach (var item in A)
                if (B.Contains(item))
                    self.Add(item);
            return modified;
        }


        public static HashSet<T> Common<T>(this HashSet<T> A, HashSet<T> B)
        {
            var self = new HashSet<T>();
            foreach (var item in A)
                if (B.Contains(item))
                    self.Add(item);
            return self;
        }


        public static bool AddRange<T>(this HashSet<T> self, HashSet<T> other)
        {
            if (other == null)
                return false;
            bool modified = false;
            foreach(var item in other)
                modified = self.Add(item) || modified;
            return modified;
        }

        public static bool AddRange<T>(this HashSet<T> self, IEnumerable<T> other)
        {
            if (other == null)
                return false;
            bool modified = false;
            foreach(var item in other)
                modified = self.Add(item) || modified;
            return modified;
        }

        public static bool RemoveRange<T>(this HashSet<T> self, HashSet<T> other)
        {
            if (other == null)
                return false;
            bool modified = false;
            foreach(var item in other)
                modified = self.Remove(item) || modified;
            return modified;
        }

        public static bool RemoveRange<T>(this HashSet<T> self, IEnumerable<T> other)
        {
            if (other == null)
                return false;
            bool modified = false;
            foreach(var item in other)
                modified = self.Remove(item) || modified;
            return modified;
        }


        public static bool ContentsEquals<T>(this HashSet<T> self, HashSet<T> other)
        {
            if (other.Count != self.Count)
                return false;

            foreach (var item in other)
                if (!self.Contains(item))
                    return false;
            return true;
        }

        public static bool ContentsEquals<T>(this HashSet<T> self, IEnumerable<T> other)
        {
            if (other.Count() != self.Count)
                return false;

            foreach (var item in other)
                if (!self.Contains(item))
                    return false;
            return true;
        }


        public static bool ContainsAll<T>(this HashSet<T> self, HashSet<T> other)
        {
            if (other.Count > self.Count)
                return false;

            foreach (var item in other)
                if (!self.Contains(item))
                    return false;
            return true;
        }

        public static bool ContainsAll<T>(this HashSet<T> self, IEnumerable<T> other)
        {
            foreach (var item in other)
                if (!self.Contains(item))
                    return false;
            return true;
        }

        public static bool ContainsAny<T>(this HashSet<T> self, IEnumerable<T> other)
        {
            foreach (var item in other)
                if (self.Contains(item))
                    return true;
            return false;
        }
    }
}
