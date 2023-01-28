#if NETSTANDARD
using Microsoft.CodeAnalysis;

namespace XmlSerDe.Generator.Helper
{
    public static class SyntaxHelper
    {
        public static SyntaxNode? Up<T>(this SyntaxNode node)
            where T : SyntaxNode
        {
            SyntaxNode? cnode = node;
            while(cnode != null)
            {
                if(cnode is T t)
                {
                    return t;
                }

                cnode = cnode.Parent;
            }

            return null;
        }
    }
}
#endif
