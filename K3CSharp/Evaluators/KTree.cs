using System;
using System.Collections.Generic;
using System.Linq;

namespace K3CSharp
{
    /// <summary>
    /// Manages the K tree namespace structure for global variables
    /// </summary>
    public class KTree
    {
        private DictionaryValue root;
        private SymbolValue currentBranch = new SymbolValue(".k"); // Initialize with default
        
        /// <summary>
        /// Gets the root dictionary of the K tree
        /// </summary>
        public DictionaryValue Root => root;
        
        /// <summary>
        /// Gets or sets the current branch (absolute path)
        /// </summary>
        public SymbolValue? CurrentBranch 
        { 
            get => currentBranch;
            set => currentBranch = value ?? new SymbolValue(""); // Default to empty symbol for root
        }
        
        public KTree()
        {
            // Initialize the root dictionary
            root = new DictionaryValue();
            
            // The initial value of .k is _n (null) per spec
            // Its type will change to dictionary when variables are added
            root.Entries[new SymbolValue("k")] = (new NullValue(), null!);
            
            // Add the .t variable to root with default value 0
            root.Entries[new SymbolValue("t")] = (new IntegerValue(0), null!);
            
            // Set default current branch to .k
            CurrentBranch = new SymbolValue(".k");
        }
        
        /// <summary>
        /// Resolves a dotted path to the actual dictionary and key
        /// </summary>
        /// <param name="path">The path to resolve (can be absolute or relative)</param>
        /// <returns>Dictionary and key, or null if not found</returns>
        public (DictionaryValue? dictionary, SymbolValue? key) ResolvePath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return (null, null);
                
            // Determine if this is an absolute or relative path
            bool isAbsolute = path.StartsWith(".");
            string[] parts;
            
            if (isAbsolute)
            {
                // For absolute paths, keep the leading underscore in the first part
                parts = path.Substring(1).Split('.', StringSplitOptions.RemoveEmptyEntries);
            }
            else
            {
                parts = path.Split('.', StringSplitOptions.RemoveEmptyEntries);
            }
            
            DictionaryValue? currentDict;
            int startIndex = 0;
            
            if (isAbsolute)
            {
                // Start from root
                currentDict = root;
                startIndex = 0; // Skip the empty part before the first dot
            }
            else
            {
                // Start from current branch
                currentDict = GetDictionaryFromPath(CurrentBranch?.Value ?? "");
                if (currentDict == null)
                    currentDict = root;
                startIndex = 0;
            }
            
            // Navigate through the path
            for (int i = startIndex; i < parts.Length - 1; i++)
            {
                string part = parts[i];
                var key = new SymbolValue(part);
                
                if (currentDict.Entries.TryGetValue(key, out var entry))
                {
                    if (entry.Value is DictionaryValue dictValue)
                    {
                        currentDict = dictValue;
                    }
                    else if (entry.Value is NullValue)
                    {
                        // Path component exists but is null - create new dictionary
                        var newDict = new DictionaryValue();
                        currentDict.Entries[key] = (newDict, null!);
                        currentDict = newDict;
                    }
                    else
                    {
                        // Path component is not a dictionary
                        return (null, null);
                    }
                }
                else
                {
                    // Path component doesn't exist - create it
                    var newDict = new DictionaryValue();
                    currentDict.Entries[key] = (newDict, null!);
                    currentDict = newDict;
                }
            }
            
            if (parts.Length > startIndex)
            {
                var finalKey = new SymbolValue(parts[parts.Length - 1]);
                return (currentDict, finalKey);
            }
            
            return (null, null);
        }
        
        /// <summary>
        /// Gets a value from the K tree using dotted notation
        /// </summary>
        /// <param name="path">The path to the value</param>
        /// <returns>The value, or null if not found</returns>
        public K3Value? GetValue(string path)
        {
            var (dict, key) = ResolvePath(path);
            if (dict != null && key != null && dict.Entries.TryGetValue(key, out var entry))
            {
                return entry.Value;
            }
            return null;
        }
        
        /// <summary>
        /// Sets a value in the K tree using dotted notation
        /// </summary>
        /// <param name="path">The path where to set the value</param>
        /// <param name="value">The value to set</param>
        /// <param name="attribute">Optional attribute dictionary</param>
        /// <returns>True if successful, false if path is invalid</returns>
        public bool SetValue(string path, K3Value value, DictionaryValue? attribute = null)
        {
            var (dict, key) = ResolvePath(path);
            if (dict != null && key != null)
            {
                dict.Entries[key] = (value, attribute!);
                return true;
            }
            return false;
        }
        
        /// <summary>
        /// Gets the parent of the current branch
        /// </summary>
        /// <returns>The parent branch symbol, or root if already at root</returns>
        public SymbolValue GetParentBranch()
        {
            string current = CurrentBranch?.Value ?? "";
            if (string.IsNullOrEmpty(current) || current == ".")
            {
                return new SymbolValue(""); // Root
            }
            
            // Remove the last component
            int lastDot = current.LastIndexOf('.');
            if (lastDot < 0)
            {
                return new SymbolValue(""); // Root
            }
            
            string parentPath = current.Substring(0, lastDot);
            if (string.IsNullOrEmpty(parentPath))
            {
                return new SymbolValue(""); // Root
            }
            
            return new SymbolValue(parentPath);
        }
        
        /// <summary>
        /// Gets all keys from the root dictionary
        /// </summary>
        /// <returns>Vector of symbol keys</returns>
        public VectorValue GetRootKeys()
        {
            var keys = root.Entries.Keys.Select(k => (K3Value)new SymbolValue(k.Value)).ToList();
            return new VectorValue(keys);
        }
        
        /// <summary>
        /// Gets the variable names in a given branch path.
        /// </summary>
        /// <param name="branchPath">Absolute path to the branch (e.g. ".k")</param>
        /// <returns>List of variable name strings</returns>
        public List<string> GetBranchVariableNames(string branchPath)
        {
            var dict = GetDictionaryFromPath(branchPath);
            if (dict == null)
                return new List<string>();

            return dict.Entries.Keys
                .Select(k => k.Value)
                .ToList();
        }

        /// <summary>
        /// Gets a dictionary from a path string
        /// </summary>
        /// <param name="path">The path to the dictionary</param>
        /// <returns>The dictionary, or null if not found</returns>
        private DictionaryValue? GetDictionaryFromPath(string path)
        {
            if (string.IsNullOrEmpty(path) || path == ".")
                return root;
                
            if (path.StartsWith("."))
                path = path.Substring(1); // Remove leading dot
                
            string[] parts = path.Split('.', StringSplitOptions.RemoveEmptyEntries);
            DictionaryValue currentDict = root;
            
            foreach (string part in parts)
            {
                var key = new SymbolValue(part);
                if (currentDict.Entries.TryGetValue(key, out var entry))
                {
                    if (entry.Value is DictionaryValue dictValue)
                    {
                        currentDict = dictValue;
                    }
                    else
                    {
                        return null; // Path component is not a dictionary
                    }
                }
                else
                {
                    return null; // Path component doesn't exist
                }
            }
            
            return currentDict;
        }
        
        /// <summary>
        /// Creates a new branch in the K tree
        /// </summary>
        /// <param name="path">The path where to create the branch</param>
        /// <returns>True if successful, false if path is invalid or already exists</returns>
        public bool CreateBranch(string path)
        {
            var (dict, key) = ResolvePath(path);
            if (dict != null && key != null)
            {
                // Check if key already exists
                if (!dict.Entries.ContainsKey(key))
                {
                    dict.Entries[key] = (new DictionaryValue(), null!);
                    return true;
                }
            }
            return false;
        }
        /// <summary>
        /// Finds the K-tree branch path for a given DictionaryValue using reference equality.
        /// Returns null if the dictionary is not found in the K-tree.
        /// </summary>
        public string? FindPath(DictionaryValue target)
        {
            return FindPathRecursive(root, target, ".");
        }

        private string? FindPathRecursive(DictionaryValue current, DictionaryValue target, string currentPath)
        {
            if (ReferenceEquals(current, target))
                return currentPath;

            foreach (var entry in current.Entries)
            {
                if (entry.Value.Value is DictionaryValue childDict)
                {
                    var childPath = currentPath == "." ? "." + entry.Key.Value : currentPath + "." + entry.Key.Value;
                    var result = FindPathRecursive(childDict, target, childPath);
                    if (result != null)
                        return result;
                }
            }
            return null;
        }

        /// <summary>
        /// Ensures that each component along the given branch path is a DictionaryValue,
        /// promoting NullValue entries to empty dictionaries as needed.
        /// </summary>
        public void EnsureBranchIsDictionary(string path)
        {
            if (string.IsNullOrEmpty(path) || path == ".")
                return;

            string trimmed = path.StartsWith(".") ? path.Substring(1) : path;
            string[] parts = trimmed.Split('.', StringSplitOptions.RemoveEmptyEntries);
            DictionaryValue currentDict = root;

            foreach (string part in parts)
            {
                var key = new SymbolValue(part);
                if (currentDict.Entries.TryGetValue(key, out var entry))
                {
                    if (entry.Value is DictionaryValue dictValue)
                    {
                        currentDict = dictValue;
                    }
                    else
                    {
                        // Promote non-dictionary (e.g. NullValue) to an empty dictionary
                        var newDict = new DictionaryValue();
                        currentDict.Entries[key] = (newDict, entry.Attribute);
                        currentDict = newDict;
                    }
                }
                else
                {
                    // Create a new branch
                    var newDict = new DictionaryValue();
                    currentDict.Entries[key] = (newDict, null!);
                    currentDict = newDict;
                }
            }
        }
    }
}
