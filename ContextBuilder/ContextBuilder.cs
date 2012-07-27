using System;
using System.Collections.Generic;
using System.Dynamic;

namespace Alphashack.Graphdat.Agent
{
    public class ContextBuilder<T> : IContextBuilder<T>
    {
        private readonly Node<T> _root;
        private readonly Action<string> _logger;
        private Node<T> _current;

        public ContextBuilder(Func<T> create = null, Action<T> finish = null, Action<string> logger = null)
        {
            _root = new Node<T>(null, null, create, finish, logger);
            _logger = logger;
            _current = _root;
        }

        internal void LogAndThrow(string message)
        {
            if (_logger != null)
            {
                _logger(message);
            }
            throw new Exception(message);
        }

        public void Enter(string name = null, Func<T> create = null, Action<T> finish = null)
        {
            var newnode = new Node<T>(name, _current, create, finish, _logger);
            _current.Children.Add(newnode);
            _current = newnode;
        }

        public T Leave(string name = null)
        {
            if (_current == _root) LogAndThrow("Context error: cannot 'Leave' from root, you might want 'Done'");
            _current.Finish(name);
            var payload = _current.Payload;
            _current = _current.Parent;
            return payload;
        }

        public T Done()
        {
            if (_current != _root) LogAndThrow("Context error: not at root when 'Done' called");
            _current.Finish();
            return _current.Payload;
        }

        public T Exit()
        {
            while (_current.Parent != null)
            {
                Leave();
            }
            return Done();
        }

        public bool Validate()
        {
            return _current == _root;
        }

        /*
        public ExpandoObject Objectify(Action<T, ExpandoObject> build = null)
        {
            if (_current != _root) LogAndThrow("Context error: not at root when 'Objectify' called");
            return _current.Objectify(build);
        }*/

        public List<ExpandoObject> Flatten(Action<T, ExpandoObject> build = null)
        {
            if (_current != _root) LogAndThrow("Context error: not at root when 'Flatten' called");
            return _current.Flatten(build);
        }
    }

    public class Node<T>
    {
        private readonly string _name;
        internal Node<T> Parent;
        internal T Payload;
        private readonly Action<T> _finish;
        private readonly Action<string> _logger;
        internal List<Node<T>> Children;

        public Node(string name = null, Node<T> parent = null, Func<T> create = null, Action<T> finish = null, Action<string> logger = null)
        {
            _name = name;
            Parent = parent;
            if(create != null)
            {
                Payload = create();
            }
            _finish = finish;
            _logger = logger;
            Children = new List<Node<T>>();
        }

        public void LogAndThrow(string message)
        {
            if (_logger != null)
            {
                _logger(message);
            }
            throw new Exception(message);
        }

        public void Finish(string name = null)
        {
            if (!string.IsNullOrEmpty(name) && _name != name)
                LogAndThrow("Context error: tried to leave '" + name + "' but current context is '" + _name + "'");
            if (_finish != null) _finish(Payload);
        }

        public ExpandoObject Build(Action<T, ExpandoObject> build = null)
        {
            dynamic obj = new ExpandoObject();
            if (build != null) build(Payload, obj);
            return obj;
        }

        /*
        public ExpandoObject Objectify(Func<T, ExpandoObject> build = null, List<ExpandoObject> list = null)
        {
            var current = Build(build);
            if (!string.IsNullOrEmpty(_name)) current.name = _name;
            foreach (var child in _children)
            {
                if (!current.children) current.children = new List<dynamic>();
                child.Objectify(build, current.children);
            }
            if (list != null) list.Add(current);
            return current;
        }*/

        public List<ExpandoObject> Flatten(Action<T, ExpandoObject> build = null, List<ExpandoObject> list = null, string path = null)
        {
            if (list == null) list = new List<ExpandoObject>();
            path = path ?? "";
            if (path != "/") path = path + "/";
            dynamic current = Build(build);
            current.Name = path = path + _name ?? "";
            list.Add(current);
            foreach (var child in Children)
            {
                child.Flatten(build, list, path);
            }
            return list;
        }

    }
}
