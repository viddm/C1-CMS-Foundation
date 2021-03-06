using System.Collections.Generic;


namespace Composite.C1Console.Forms.Foundation.FormTreeCompiler.CompileTreeNodes
{
    /// <summary>    
    /// </summary>
    /// <exclude />
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)] 
    public class ElementCompileTreeNode : CompileTreeNode
    {
        private object _producer = null;
        private Dictionary<int, List<PropertyCompileTreeNode>> _addedProperties = new Dictionary<int, List<PropertyCompileTreeNode>>();

        /// <exclude />
        public ElementCompileTreeNode(XmlSourceNodeInformation sourceInformation)
            : base(sourceInformation)
        {
        }

        /// <exclude />
        public object Producer
        {
            get { return _producer; }
            set { _producer = value; }
        }

        /// <exclude />
        public Dictionary<int, List<PropertyCompileTreeNode>> AddedProperties
        {
            get { return _addedProperties; }
        }
    }
}
