using System;
using System.Collections.Generic;
using System.Text;

namespace JsonLD.Core
{
    class IdentifierIssuer 
    {
        private string prefix;
        private int counter;
        private Dictionary<string, string> existing;
        private IList<string> order;

		public  IdentifierIssuer(string prefix)
		{
			/*
			* Initializes a new IdentifierIssuer.
			* :param prefix: the prefix to use ('<prefix><counter>').
			*/

			this.prefix = prefix;
			this.counter = 0;
			this.existing =  new Dictionary<string, string>();
			this.order = new List<string>();

			/*
			* Gets the new identifier for the given old identifier, where if no old
			* identifier is given a new identifier will be generated.
			* :param [old]: the old identifier to get the new identifier for.
			* :return: the new identifier.
			*/
		}

		public string getId()
		{
			return this.getId(null);
		}

		public string getId(string old)
		{
			if (old != null && existing.ContainsKey(old))
			{
				return this.existing[old];
			}

			string id = this.prefix + counter.ToString();
			this.counter += 1;

			if (old != null)
			{

				/*
				* Returns True if the given old identifier has already been assigned a
				* new identifier.
				* :param old: the old identifier to check.
				* :return: True if the old identifier has been assigned a new identifier,
				* False if not.
				*/

				this.existing.Add(old, id);
				this.order.Add(old);

			}

			return id; 

		}

		public Boolean hasID(string old)
		{
			return this.existing.ContainsKey(old);
		}

		public IList<string> getOrder()
		{
			return this.order;
		}

		public string getPrefix()
		{
			return this.prefix;
		}

		public object Clone()
		{
			try
			{

				return new IdentifierIssuer(this.prefix);

			}
			catch (Exception ex)
			{
				Console.Out.WriteLine(ex);
				return null;
			}
		}

	}
}
