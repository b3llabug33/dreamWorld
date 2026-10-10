using System;
using System.Collections.Generic;

namespace DreamWorld.Quests
{
    // A conversation: named nodes, each with a speaker line and either a next node, a set of choices,
    // or an end. Actions run when a node is shown (give an item, set a quest flag, advance a quest).
    public class Dialogue
    {
        public class Choice
        {
            public string label;
            public string next;          // node id, or null to end the conversation
            public Action onPick;
        }

        public class Node
        {
            public string speaker;
            public string text;
            public string next;          // used when there are no choices (null = end)
            public List<Choice> choices = new List<Choice>();
            public Action onShow;
        }

        public readonly Dictionary<string, Node> nodes = new Dictionary<string, Node>();
        public string start;
        public Action onEnd;

        // ---------- builder helpers so quest scripts read like a screenplay ----------

        public Dialogue Line(string id, string speaker, string text, string next = null, Action onShow = null)
        {
            nodes[id] = new Node { speaker = speaker, text = text, next = next, onShow = onShow };
            if (start == null) start = id;
            return this;
        }

        public Dialogue Ask(string id, string speaker, string text, params (string label, string next)[] options)
        {
            var node = new Node { speaker = speaker, text = text };
            foreach (var (label, next) in options) node.choices.Add(new Choice { label = label, next = next });
            nodes[id] = node;
            if (start == null) start = id;
            return this;
        }

        // A plain run of lines, one after another, starting at the first.
        public static Dialogue Lines(string speaker, params string[] texts)
        {
            var d = new Dialogue();
            for (int i = 0; i < texts.Length; i++)
                d.Line("l" + i, speaker, texts[i], i + 1 < texts.Length ? "l" + (i + 1) : null);
            return d;
        }
    }
}
