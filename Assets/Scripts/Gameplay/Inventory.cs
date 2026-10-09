using System;

namespace DreamWorld.Gameplay
{
    // One slot's contents. Empty = id "" and count 0 (this way JsonUtility saves it cleanly).
    [Serializable]
    public class ItemStack
    {
        public string id = "";
        public int count;
        public bool Empty => string.IsNullOrEmpty(id) || count <= 0;
        public void Clear() { id = ""; count = 0; }
    }

    // Slot-based storage used for the bag (28) and the home storage (56). Items stack up to their maxStack.
    public static class Inventory
    {
        public const int BagSize = 28;
        public const int StorageSize = 56;

        public static ItemStack[] Make(int size)
        {
            var slots = new ItemStack[size];
            for (int i = 0; i < size; i++) slots[i] = new ItemStack();
            return slots;
        }

        // Makes sure a saved array exists and has the right size (older saves had none).
        public static ItemStack[] Fit(ItemStack[] slots, int size)
        {
            var fitted = Make(size);
            if (slots != null)
                for (int i = 0; i < slots.Length && i < size; i++)
                    if (slots[i] != null && !slots[i].Empty && Items.Get(slots[i].id) != null)
                        fitted[i] = new ItemStack { id = slots[i].id, count = slots[i].count };
            return fitted;
        }

        public static int Count(ItemStack[] slots, string id)
        {
            int n = 0;
            foreach (var s in slots) if (!s.Empty && s.id == id) n += s.count;
            return n;
        }

        // Adds as many as fit (topping up existing stacks first); returns how many were added.
        public static int Add(ItemStack[] slots, string id, int count)
        {
            var def = Items.Get(id);
            if (def == null || count <= 0) return 0;
            int left = count;
            foreach (var s in slots)
            {
                if (left == 0) break;
                if (s.Empty || s.id != id || s.count >= def.maxStack) continue;
                int add = Math.Min(left, def.maxStack - s.count);
                s.count += add;
                left -= add;
            }
            foreach (var s in slots)
            {
                if (left == 0) break;
                if (!s.Empty) continue;
                int add = Math.Min(left, def.maxStack);
                s.id = id;
                s.count = add;
                left -= add;
            }
            return count - left;
        }

        public static bool Remove(ItemStack[] slots, string id, int count)
        {
            if (Count(slots, id) < count) return false;
            for (int i = slots.Length - 1; i >= 0 && count > 0; i--)   // take from the last stacks first
            {
                var s = slots[i];
                if (s.Empty || s.id != id) continue;
                int take = Math.Min(count, s.count);
                s.count -= take;
                count -= take;
                if (s.count == 0) s.Clear();
            }
            return true;
        }

        // Click-to-move: drop what's in `from[a]` onto `to[b]`. Same item merges (up to maxStack),
        // anything else swaps places.
        public static void Move(ItemStack[] from, int a, ItemStack[] to, int b)
        {
            if (from == to && a == b) return;
            var src = from[a];
            var dst = to[b];
            if (src.Empty) return;
            if (!dst.Empty && dst.id == src.id)
            {
                int max = Items.Get(src.id).maxStack;
                int add = Math.Min(src.count, max - dst.count);
                dst.count += add;
                src.count -= add;
                if (src.count == 0) src.Clear();
                if (add > 0) return;
            }
            from[a] = dst;
            to[b] = src;
        }

        // Moves a whole stack into the first place it fits in another inventory (right-click transfer).
        public static void Transfer(ItemStack[] from, int a, ItemStack[] to)
        {
            var src = from[a];
            if (src.Empty) return;
            int moved = Add(to, src.id, src.count);
            src.count -= moved;
            if (src.count == 0) src.Clear();
        }
    }
}
