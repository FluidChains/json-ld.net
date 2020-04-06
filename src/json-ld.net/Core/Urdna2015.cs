using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using JsonLD.Core;
using Newtonsoft.Json.Linq;
using Newtonsoft;
using Newtonsoft.Json;
using System.Text;

namespace JsonLD.Core
{
    public class Urdna2015
    {


        public static Dictionary<string, string> QUAD_POSITIONS = new Dictionary<string, string>
        {
            {"subject", "s" },
            {"object","o" },
            {"name","g"}
        };

        private IList<IDictionary<string, IDictionary<string, string>>> quads;
        private IDictionary<string, IDictionary<string, IList<Object>>> blankNodeInfo;
        private IDictionary<string, IList<string>> hashToBlankNodes;
        private IdentifierIssuer canonicalIssuer;

        private IDictionary<string, Object> dataset;
        private JsonLdOptions options;


        public Urdna2015(Dictionary<string, Object> dataset, JsonLdOptions options)
        {
            this.dataset = dataset;
            this.options = options;
        }

        public Object Normalize()
        {
            this.quads = new List<IDictionary<string, IDictionary<string, string>>>();
            this.blankNodeInfo = new Dictionary<string, IDictionary<string, IList<object>>>();
            this.hashToBlankNodes = new Dictionary<string, IList<string>>();
            this.canonicalIssuer = new IdentifierIssuer("_:c14n");

            /*
			* 2) For every quad in input dataset:
			* STATUS : step 2 is good!
			*/

            foreach (string graphName in this.dataset.Keys)
            {
                IList<IDictionary<string, IDictionary<string, string>>> triples = (IList<IDictionary<string, IDictionary<string, string>>>)this.dataset[graphName];

                if (graphName.Equals("@default"))
                {
                    graphName.Replace("@default", null);
                }

                foreach (IDictionary<string, IDictionary<string, string>> quad in triples)
                {
                    if (!string.ReferenceEquals(graphName, null))
                    {
                        if (graphName.StartsWith("_:", StringComparison.Ordinal))
                        {
                            IDictionary<string, string> tmp = new Dictionary<string, string>();
                            tmp["type"] = "blank node";
                            quad["name"] = tmp;
                        }
                        else
                        {
                            IDictionary<string, string> tmp = new Dictionary<string, string>();
                            tmp["type"] = "IRI";
                            quad["name"] = tmp;
                        }
                        quad["name"]["value"] = graphName;
                    }
                    this.quads.Add(quad);

                    /* 2.1) For each blank node that occurs in the quad, add a
					* reference to the quad using the blank node identifier in the
					* blank node to quads map, creating a new entry if necessary.
					* */

                    foreach (string key in quad.Keys)
                    {

                        Dictionary<string, string> component = (Dictionary<string, string>)quad[key];
                        if (key.Equals("predicate") || !component["type"].Equals("blank node"))
                        {
                            continue;
                        }
                        string id = component["value"];
                        if (this.blankNodeInfo[id] == null)
                        {
                            IDictionary<string, IList<Object>> quadList = new Dictionary<string, IList<Object>>();
                            quadList["quads"] = new List<Object>();
                            quadList["quads"].Add(quad);
                            this.blankNodeInfo[id] = quadList;
                        }
                        else
                        {
                            this.blankNodeInfo[id]["quads"].Add(quad);
                        }
                    }
                }

                List<string> nonNormalized = new List<string>();
                nonNormalized.AddRange(blankNodeInfo.Keys);

                //Collections.sort(nonNormalized);

                /* 4) Initialize simple, a boolean flag, to true.
			    * STATUS : if this does not work we have a serious problem
			    */
                bool simple = true;

                /*
				* 5) While simple is true, issue canonical identifiers for blank nodes:
				*/
                while (simple)
                {
                    // 5.1) Set simple to false.
                    simple = false;

                    // 5.2) Clear hash to blank nodes map.
                    this.hashToBlankNodes.Clear();

                    /*
					* 5.3) For each blank node identifier identifier in non-normalized
					* identifiers:
					* STATUS : working on it
					*/
                    foreach (string id in nonNormalized)
                    {
                        string hash = hashFirstDegreeQuads(id);

                        if (this.hashToBlankNodes.ContainsKey(hash))
                        {
                            this.hashToBlankNodes[hash].Add(id);
                        }
                        else
                        {
                            List<string> idList = new List<string>();
                            idList.Add(id);
                            this.hashToBlankNodes.Add(hash, idList);
                        }
                    }

                    /*
					* 5.4) For each hash to identifier list mapping in hash to blank
					* nodes map, lexicographically-sorted by hash:
					*/

                    foreach (string hash in sortMapKeys(this.hashToBlankNodes))
                    {
                        IList<string> idList = this.hashToBlankNodes[hash];
                        if (idList.Count() > 1)
                        {
                            continue;
                        }

                        /* 5.4.2) Use the Issue Identifier algorithm, passing canonical
						* issuer and the single blank node identifier in identifier
						* list, identifier, to issue a canonical replacement identifier
						* for identifier.
						*/

                        string id = idList[0];
                        this.canonicalIssuer.getId(id);

                        // 5.4.3) Remove identifier from non-normalized identifiers.
                        nonNormalized.Remove(id);

                        // 5.4.4) Remove hash from the hash to blank nodes map.

                        this.hashToBlankNodes.Remove(hash);

                        //  5.4.5) Set simple to true.

                        simple = true;

                    }
                }

                /*
				* 6) For each hash to identifier list mapping in hash to blank nodes
				* map, lexicographically-sorted by hash:
				* STATUS: does not loop through it
				*/
                foreach (string hash in sortMapKeys(this.hashToBlankNodes))
                {
                    IList<string> idList = this.hashToBlankNodes[hash];

                    /*
					* 6.1) Create hash path list where each item will be a result of
					* running the Hash N-Degree Quads algorithm.
					*/
                    var hashPathList = new List<IDictionary<string, object>>();

                    /*
					* 6.2) For each blank node identifier identifier in identifier
					* list:
					*/
                    foreach (string id in idList)
                    {
                        /*
						* 6.2.1) If a canonical identifier has already been issued for
						* identifier, continue to the next identifier.
						*/

                        if (this.canonicalIssuer.hasID(id))
                        {
                            continue;
                        }

                        /*
						* 6.2.2) Create temporary issuer, an identifier issuer
						* initialized with the prefix _:b.
						*/

                        IdentifierIssuer issuer = new IdentifierIssuer("_:b");

                        /*
						* 6.2.3) Use the Issue Identifier algorithm, passing temporary
						* issuer and identifier, to issue a new temporary blank node
						* identifier for identifier.
						*/

                        issuer.getId(id);

                        /*
					   * 6.2.4) Run the Hash N-Degree Quads algorithm, passing
					   * temporary issuer, and append the result to the hash path
					   * list.
					   */

                        hashPathList.Add(hashNDegreeQuads(issuer, id));

                    }


                    /*
					* 6.3) For each result in the hash path list,
					* lexicographically-sorted by the hash in result:
					*/

                    sortMapList(hashPathList);
                    foreach (var result in hashPathList)
                    {
                        if (result["issuer"] != null)
                        {
                            foreach (var existing in ((IdentifierIssuer)result["issuer"]).getOrder())
                            {
                                this.canonicalIssuer.getId(existing);
                            }
                        }
                    }
                }

                /*
				* Note: At this point all blank nodes in the set of RDF quads have been
				* assigned canonical identifiers, which have been stored in the
				* canonical issuer. Here each quad is updated by assigning each of its
				* blank nodes its new identifier.
				*/

                // 7) For each quad, quad, in input dataset:
                List<string> normalized = new List<string>();
                foreach (var quadMap in this.quads)
                {
                    /*
					* Create a copy, quad copy, of quad and replace any existing
					* blank node identifiers using the canonical identifiers previously
					* issued by canonical issuer. Note: We optimize away the copy here.
					* STATUS : currently working on it
					*/
                    foreach (var key in quadMap.Keys)
                    {
                        if (key.Equals("predicate"))
                        {
                            continue;
                        }
                        else
                        {
                            var component = quadMap[key];
                            if (component["type"].Equals("blank node") && !component["value"].StartsWith(this.canonicalIssuer.getPrefix()))
                            {
                                component.Add("value", this.canonicalIssuer.getId(component["value"]));

                            }
                        }
                    }

                    //  7.2) Add quad copy to the normalized dataset.
                    RDFDataset.Quad quad = new RDFDataset.Quad(quadMap, quadMap.ContainsKey("name") && quadMap["name"] != null
                        ? (quadMap["name"])["value"] : null);
                    normalized.Add(RDFDatasetUtils.ToNQuad(quad, quadMap.ContainsKey("name") && quadMap["name"] != null
                        ? (quadMap["name"])["value"] : null));

                }

                // 8) Return the normalized dataset.
                Collections.SortInPlace(normalized);
                if (this.options.format != null)
                {
                    if ("applications/nquads".Equals(this.options.format))
                    {
                        StringBuilder rval = new StringBuilder();
                        foreach (var n in normalized)
                        {
                            rval.Append(n);
                        }
                        return rval.ToString();
                    }
                    else
                    {
                        // will need to implement error handling
                        return null;
                    }
                }
                else
                {
                    StringBuilder rval = new StringBuilder();
                    foreach (var n in normalized)
                    {
                        rval.Append(n);
                    }
                    try
                    {
                        
                        return RDFDatasetUtils.ParseNQuads(rval.ToString());
                    }
                    catch (Exception ex)
                    {

                        Console.Out.WriteLine(ex);
                        return ex;
                    }
                }
            }
            return null;
        }

        /*
		* STATUS : working on it
		*/
        private string hashFirstDegreeQuads(string id)
        {
            IDictionary<string, IList<Object>> info = this.blankNodeInfo[id];
            if (info.ContainsKey("hash"))
            {
                return info["hash"].ToString();
            }

            // 1) Initialize nquads to an empty list. It will be used to store quads
            // in N-Quads format.
            IList<string> nquads = new List<string>();

            // 2) Get the list of quads quads associated with the reference blank
            // node identifier in the blank node to quads map.

            IList<Object> quads = info["quads"];

            // 3) For each quad quad in quads:
            foreach (var quad in quads)
            {
                // 3.1) Serialize the quad in N-Quads format with the following
                // special rule:

                // 3.1.1) If any component in quad is an blank node, then serialize
                // it using a special identifier as follows:

                // copy = {}

                IDictionary<string, IDictionary<string, string>> copy = new Dictionary<string, IDictionary<string, string>>();

                /* 3.1.2) If the blank node's existing blank node identifier
			   * matches the reference blank node identifier then use the
			   * blank node identifier _:a, otherwise, use the blank node
			   * identifier _:z.
			   * STATUS: working
			   */

                RDFDataset.Quad quadMap = (RDFDataset.Quad)quad;
                foreach (var key in quadMap)
                {
                    IDictionary<string, string> component = new Dictionary<string, string>();
                    component.Add(key.Key, key.Value.ToString());
                    if (key.Equals("predicate"))
                    {
                        copy.Add(key.Key, component);
                        continue;
                    }
                    copy.Add(key.Key, modifyFirstDegreeComponent(component, id));
                }

                RDFDataset.Quad copyQuad = new RDFDataset.Quad(copy, copy.ContainsKey("name") && copy["name"] != null
                    ? (copy["name"])["value"] : null);
                nquads.Add(RDFDatasetUtils.ToNQuad(copyQuad, copyQuad.ContainsKey("name") && copyQuad["name"] != null
                    ? (string)((IDictionary<string, object>)copyQuad["name"])["value"] : null));

                // 4) Sort nquads in lexicographical order.
            }

            Collections.SortInPlace(nquads);

            // 5) Return the hash that results from passing the sorted, joined
            // nquads through the hash algorithm.

            return NormalizeUtils.sha256HashnQuads(nquads);

        }

        private IDictionary<string, Object> hashNDegreeQuads(IdentifierIssuer issuer, string id)
        {
            /*
            * 1) Create a hash to related blank nodes map for storing hashes that
            * identify related blank nodes.
            * Note: 2) and 3) handled within `createHashToRelated`
            */

            IDictionary<string, IList<string>> hashToRelated = this.createHashToRelated(issuer, id);

            /*
            * 4) Create an empty string, data to hash.
            * Note: We create a hash object instead.
            */

            string mdString = "";

            /*
            * 5) For each related hash to blank node list mapping in hash to
            * related blank nodes map, sorted lexicographically by related hash:
            */
            sortMapKeys(hashToRelated);
            foreach (var hash in hashToRelated.Keys)
            {
                var blankNodes = hashToRelated[hash];
                // 5.1) Append the related hash to the data to hash.
                mdString += hash;

                // 5.2) Create a string chosen path.

                string chosenPath = " ";

                // 5.3) Create an unset chosen issuer variable.

                IdentifierIssuer chosenIssuer = null;

                // 5.4) For each permutation of blank node list:

                string path = "";
                List<string> recursionList = null;
                IdentifierIssuer issuerCopy = null;
                bool skipToNextPerm = false;
                NormalizeUtils.Permutator permutator = new NormalizeUtils.Permutator(JArray.FromObject(blankNodes));

                while (permutator.HasNext())
                {
                    var permutation = permutator.Next();
                    // 5.4.1) Create a copy of issuer, issuer copy.

                    issuerCopy = (IdentifierIssuer)issuer.Clone();

                    // 5.4.2) Create a string path.

                    path = "";

                    /*
                    * 5.4.3) Create a recursion list, to store blank node
                    * identifiers that must be recursively processed by this
                    * algorithm.
                     */

                    recursionList = new List<string>();

                    // 5.4.4) For each related in permutation:
                    foreach (var related in permutation)
                    {
                        /*
                       * 5.4.4.1) If a canonical identifier has been issued for
                       * related, append it to path.
                       */

                        if (this.canonicalIssuer.hasID(related.ToString()))
                        {
                            path += this.canonicalIssuer.getId(related.ToString());

                        }
                        // 5.4.4.2) Otherwise:
                        else
                        {

                            /*
                            * 5.4.4.2.1) If issuer copy has not issued an
                            * identifier for related, append related to recursion
                            * list.
                            */

                            if (!issuerCopy.hasID(related.ToString()))
                            {
                                recursionList.Add(related.ToString());
                            }

                            /*
                           * 5.4.4.2.2) Use the Issue Identifier algorithm,
                           * passing issuer copy and related and append the result
                           * to path.
                           */

                            path += issuerCopy.getId(related.ToString());

                        }

                        /*
                        * 5.4.4.3) If chosen path is not empty and the length of
                        * path is greater than or equal to the length of chosen
                        * path and path is lexicographically greater than chosen
                        * path, then skip to the next permutation.
                        */

                        if (chosenPath.Length != 0 && path.Length >= chosenPath.Length && path.CompareTo(chosenPath) == 1)
                        {
                            skipToNextPerm = true;
                            break;
                        }
                    }
                }

                if (skipToNextPerm)
                {
                    continue;
                }

                // 5.4.5) For each related in recursion list:

                foreach (var related in recursionList)
                {
                    /*
                    * 5.4.5.1) Set result to the result of recursively
                    * executing the Hash N-Degree Quads algorithm, passing
                    * related for identifier and issuer copy for path
                    * identifier issuer.
                    */
                    IDictionary<string, object> result = hashNDegreeQuads(issuerCopy, related);

                    /*
                     * 5.4.5.2) Use the Issue Identifier algorithm, passing
                     * issuer copy and related and append the result to path.
                     */

                    path += "<" + (string)result["hash"] + ">";

                    /*
                    * 5.4.5.4) Set issuer copy to the identifier issuer in
                    * result.
                    */

                    issuerCopy = (IdentifierIssuer)result["issuer"];

                    /*
                   * 5.4.5.5) If chosen path is not empty and the length of
                   * path is greater than or equal to the length of chosen
                   * path and path is lexicographically greater than chosen
                   * path, then skip to the next permutation.
                   */

                    if (chosenPath.Length != 0 && path.Length >= chosenPath.Length && path.CompareTo(chosenPath) == 1)
                    {
                        skipToNextPerm = true;
                        break;

                    }
                }

                if (skipToNextPerm)
                {
                    continue;
                }

                /*
                * 5.4.6) If chosen path is empty or path is lexicographically
                * less than chosen path, set chosen path to path and chosen
                * issuer to issuer copy.
                */

                if (chosenPath.Length == 0 || path.CompareTo(chosenPath) == -1)
                {
                    chosenPath = path;
                    chosenIssuer = issuerCopy;
                }

                // 5.5) Append chosen path to data to hash.
                mdString += chosenPath;

                // 5.6) Replace issuer, by reference, with chosen issuer.
                issuer = chosenIssuer;

                /*
                6) Return issuer and the hash that results from passing data to hash
                * through the hash algorithm.
                */
            }

            /*
           * 6) Return issuer and the hash that results from passing data to hash
           * through the hash algorithm.
           */

            IDictionary<string, object> hashQuad = new Dictionary<string, object>();
            hashQuad.Add("hash", NormalizeUtils.sha256Hash(Encoding.ASCII.GetBytes(mdString)));
            hashQuad.Add("issuer", issuer);

            return hashQuad;
            
        }

        private IDictionary<string, IList<string>> createHashToRelated(IdentifierIssuer issuer, string id)
        {
            /*
           * 1) Create a hash to related blank nodes map for storing hashes that
           * identify related blank nodes.
           */

            IList<object> quads = this.blankNodeInfo[id]["quads"];
            IDictionary<string, IList<string>> hashToRelated = new Dictionary<string, IList<string>>();

            /*
            * 2) Get a reference, quads, to the list of quads in the blank node to
            * quads map for the key identifier.
            * Already in parameter
            */

            // 3) For each quad in quads:

            foreach (var quad in quads)
            {
                /*
                * 3.1) For each component in quad, if component is the subject,
                * object, and graph name and it is a blank node that is not
                * identified by identifier:
                */

                IDictionary<string, IDictionary<string, string>> quadMap = (IDictionary<string, IDictionary<string, string>>)quad;

                foreach (var key in quadMap.Keys)
                {
                    IDictionary<string, string> component = quadMap[key];
                    if(!key.Equals("predicate") && component["type"].Equals("blank node") && !component["value"].Equals(id))
                    {
                        /*
                       * 3.1.1) Set hash to the result of the Hash Related Blank
                       * Node algorithm, passing the blank node identifier for
                       * component as related, quad, path identifier issuer as
                       * issuer, and position as either s, o, or g based on
                       * whether component is a subject, object, graph name,
                       * respectively.
                       */

                        string related = component["value"];
                        string position = QUAD_POSITIONS[key];

                        string hash = hashRelateBlankNode(related, quadMap, issuer, position);

                        if (hashToRelated.ContainsKey(hash))
                        {
                            hashToRelated[hash].Add(related);
                        }
                        else {
                            List<string> relatedList = new List<string>();
                            relatedList.Add(related);
                            hashToRelated.Add(hash, relatedList);
                        }
                    }
                }

            }
            return hashToRelated;

        }

        private string hashRelateBlankNode(string related, IDictionary<string, IDictionary<string, string>> quad, IdentifierIssuer issuer, string position)
        {
            /*
            * 1) Set the identifier to use for related, preferring first the
            * canonical identifier for related if issued, second the identifier
            * issued by issuer if issued, and last, if necessary, the result of
            * the Hash First Degree Quads algorithm, passing related.
            */
            string id;
            if (this.canonicalIssuer.hasID(related))
            {
                id = this.canonicalIssuer.getId(related);
            }
            else if (issuer.hasID(related))
            {
                id = issuer.getId(related);
            }
            else {
                id = hashFirstDegreeQuads(related);
            }

            /*
            * 2) Initialize a string input to the value of position.
            * Note: We use a hash object instead.
            */

            if (!position.Equals("g"))
            {
                return NormalizeUtils.sha256Hash(Encoding.ASCII.GetBytes(position + getRelatedPredicate(quad) + id));
            }
            else {
                return NormalizeUtils.sha256Hash(Encoding.ASCII.GetBytes(position + id));
            }
        }

        private static IDictionary<string, string> modifyFirstDegreeComponent(IDictionary<string, string> component, string id)
        {

            if (!component["type"].Equals("blank node"))
            {
                return component;
            }

            IDictionary<string, string> componentClone = (IDictionary<string, string>)JsonLdUtils.Clone(JToken.FromObject(component));
            if (componentClone["value"].Equals(id))
            {
                componentClone["value"] = "_:a";
            }
            else
            {
                componentClone["value"] = "_:z";
            }
            return componentClone;
        }

        private string getRelatedPredicate(IDictionary<string, IDictionary<string, string>> quad)
        {
            return "<" + quad["predicate"]["value"] + ">";

        }      


        public static IList<string> sortMapKeys(IDictionary<string, IList<string>> map)
        { // need to reverse list
            IList<string> keyList = new List<string>(map.Keys);
            keyList.SortInPlace(StringComparer.Ordinal);

            return keyList;
        }

        public static IList<IDictionary<string, object>> sortMapList(IList<IDictionary<string, object>> mapList)
        {
            return sortMapList(mapList, true);
        }

        public static IList<IDictionary<string, object>> sortMapList(IList<IDictionary<string, object>> mapList, bool recursion)
        {
            IList<IDictionary<string, object>> sortedMapsList = new List<IDictionary<string, object>>();
            foreach (IDictionary<string, object> map in mapList)
            {
                IDictionary<string, object> newMap = new Dictionary<string, object>();
                IList<string> keyList = new List<string>(map.Keys);
                keyList.SortInPlace(StringComparer.Ordinal);

                foreach (string key in keyList)
                {
                    newMap[key] = map[key];
                }
                sortedMapsList.Add(newMap);
            }
            if (recursion)
            {
                return sortMapList(sortedMapsList, false);
            }
            return sortedMapsList;

        }



        



    }
}
