﻿using Models.Core;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Models.WholeFarm.Resources
{
	///<summary>
	/// Store for manure
	///</summary> 
	[Serializable]
	[ViewName("UserInterface.Views.GridView")]
	[PresenterName("UserInterface.Presenters.PropertyPresenter")]
	[ValidParent(ParentType = typeof(ProductStore))]
	public class ProductStoreTypeManure: WFModel, IResourceWithTransactionType, IResourceType
    {
        /// <summary>
        /// List of all uncollected manure stores
        /// These present manure in the field and yards
        /// </summary>
        [NonSerialized]
        public List<ManureStoreUncollected> UncollectedStores;

        /// <summary>
        /// Biomass decay rate each time step
        /// </summary>
        [Description("Biomass decay rate each time step")]
        [Required, Range(0, 100, ErrorMessage = "Value must be a proportion in the range 0 to 1")]
        public double DecayRate { get; set; }

		/// <summary>
		/// Moisture decay rate each time step
		/// </summary>
		[Description("Moisture decay rate each time step")]
        [Required, Range(0, 100, ErrorMessage = "Value must be a proportion in the range 0 to 1")]
        public double MoistureDecayRate { get; set; }

		/// <summary>
		/// Proportion moisture of fresh manure
		/// </summary>
		[Description("Proportion moisture of fresh manure")]
        [Required, Range(0, 100, ErrorMessage = "Value must be a proportion in the range 0 to 1")]
        public double ProportionMoistureFresh { get; set; }

		/// <summary>
		/// Maximum age manure lasts
		/// </summary>
		[Description("Maximum age (time steps) manure lasts")]
        [Required]
        public int MaximumAge { get; set; }

        /// <summary>An event handler to allow us to initialise ourselves.</summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
		[EventSubscribe("WFInitialiseResource")]
        private void OnWFInitialiseResource(object sender, EventArgs e)
        {
            UncollectedStores = new List<ManureStoreUncollected>();
        }

        /// <summary>
        /// Method to add uncollected manure to stores
        /// </summary>
        /// <param name="storeName">Name of store to add manure to</param>
        /// <param name="amount">Amount (dry weight) of manure to add</param>
        public void AddUncollectedManure(string storeName, double amount)
		{
			ManureStoreUncollected store = UncollectedStores.Where(a => a.Name.ToLower() == storeName.ToLower()).FirstOrDefault();
			if(store == null)
			{
				store = new ManureStoreUncollected() { Name = storeName };
				UncollectedStores.Add(store);
			}
			ManurePool pool = store.Pools.Where(a => a.Age == 0).FirstOrDefault();
			if(pool == null)
			{
				pool = new ManurePool() { Age = 0, ProportionMoisture= ProportionMoistureFresh };
				store.Pools.Add(pool);
			}
			pool.Amount += amount;
		}

		/// <summary>
		/// Function to age manure pools
		/// </summary>
		/// <param name="sender">The sender.</param>
		/// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
		[EventSubscribe("WFAgeResources")]
		private void OnWFAgeResources(object sender, EventArgs e)
		{
			// decay N and DMD of pools and age by 1 month
			foreach (ManureStoreUncollected store in UncollectedStores)
			{
				foreach (ManurePool pool in store.Pools)
				{
					pool.Age++;
					pool.Amount *= DecayRate;
                    pool.ProportionMoisture *= MoistureDecayRate;
				}
				store.Pools.RemoveAll(a => a.Age > MaximumAge);
			}
		}

		/// <summary>
		/// Method to collect manure from uncollected manure stores
		/// Manure is collected from freshest to oldest
		/// </summary>
		/// <param name="storeName">Name of store to add manure to</param>
		/// <param name="resourceLimiter">Reduction due to limited resources</param>
		/// <param name="activityName">Name of activity performing collection</param>
		public void Collect(string storeName, double resourceLimiter, string activityName)
		{
			ManureStoreUncollected store = UncollectedStores.Where(a => a.Name.ToLower() == storeName.ToLower()).FirstOrDefault();
			if (store != null)
			{
				double limiter = Math.Max(Math.Min(resourceLimiter, 1.0), 0);
				double amountPossible = store.Pools.Sum(a => a.Amount) * limiter;
				double amountMoved = 0;

				while (store.Pools.Count > 0 && amountMoved<amountPossible)
				{
					// take needed
					double take = Math.Min(amountPossible - amountMoved, store.Pools[0].Amount);
					amountMoved += take;
					store.Pools[0].Amount -= take; 
					// if 0 delete
					store.Pools.RemoveAll(a => a.Amount == 0);
				}
				this.Add(amountMoved, activityName, ((storeName=="")?"General":storeName));
			}
		}

        private double amount;
        /// <summary>
        /// Current amount of this resource
        /// </summary>
        public double Amount { get { return amount; } }

        /// <summary>An event handler to allow us to initialise ourselves.</summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        [EventSubscribe("WFInitialiseResource")]
        private void OnWFInitialiseResource(object sender, EventArgs e)
        {
            Initialise();
        }

        /// <summary>
        /// Initialise resource type
        /// </summary>
        public void Initialise()
        {
            this.amount = 0;
            //if (StartingAmount > 0)
            //{
            //    Add(StartingAmount, this.Name, "Starting value");
            //}
        }



        #region transactions

        /// <summary>
        /// Back account transaction occured
        /// </summary>
        public event EventHandler TransactionOccurred;

        /// <summary>
        /// Transcation occurred 
        /// </summary>
        /// <param name="e"></param>
        protected virtual void OnTransactionOccurred(EventArgs e)
        {
            var h = TransactionOccurred; if (h != null) h(this, e);
        }

        /// <summary>
        /// Last transaction received
        /// </summary>
        [XmlIgnore]
        public ResourceTransaction LastTransaction { get; set; }

        /// <summary>
        /// Add money to account
        /// </summary>
        /// <param name="ResourceAmount"></param>
        /// <param name="ActivityName"></param>
        /// <param name="Reason"></param>
        public void Add(object ResourceAmount, string ActivityName, string Reason)
        {
            if (ResourceAmount.GetType().ToString() != "System.Double")
            {
                throw new Exception(String.Format("ResourceAmount object of type {0} is not supported Add method in {1}", ResourceAmount.GetType().ToString(), this.Name));
            }
            double addAmount = (double)ResourceAmount;
            if (addAmount > 0)
            {
                amount += addAmount;

                ResourceTransaction details = new ResourceTransaction();
                details.Credit = addAmount;
                details.Activity = ActivityName;
                details.Reason = Reason;
                details.ResourceType = this.Name;
                LastTransaction = details;
                TransactionEventArgs te = new TransactionEventArgs() { Transaction = details };
                OnTransactionOccurred(te);
            }
        }

        /// <summary>
        /// Remove money (object) from account
        /// </summary>
        /// <param name="RemoveRequest"></param>
        public void Remove(object RemoveRequest)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Remove from finance type store
        /// </summary>
        /// <param name="Request">Resource request class with details.</param>
        public void Remove(ResourceRequest Request)
        {
            if (Request.Required == 0) return;
            // avoid taking too much
            double amountRemoved = Request.Required;
            amountRemoved = Math.Min(this.Amount, amountRemoved);
            this.amount -= amountRemoved;

            Request.Provided = amountRemoved;
            ResourceTransaction details = new ResourceTransaction();
            details.ResourceType = this.Name;
            details.Debit = amountRemoved * -1;
            details.Activity = Request.ActivityModel.Name;
            details.Reason = Request.Reason;
            LastTransaction = details;
            TransactionEventArgs te = new TransactionEventArgs() { Transaction = details };
            OnTransactionOccurred(te);
        }

        /// <summary>
        /// Set the amount in an account.
        /// </summary>
        /// <param name="NewAmount"></param>
        public void Set(double NewAmount)
        {
            amount = NewAmount;
        }

        #endregion








        ///// <summary>
        ///// Back account transaction occured
        ///// </summary>
        //public event EventHandler TransactionOccurred;

        ///// <summary>
        ///// Transcation occurred 
        ///// </summary>
        ///// <param name="e"></param>
        //protected virtual void OnTransactionOccurred(EventArgs e)
        //{
        //    if (TransactionOccurred != null)
        //        TransactionOccurred(this, e);
        //}

        //public void Add(object ResourceAmount, string ActivityName, string Reason)
        //{
        //    throw new NotImplementedException();
        //}

        //public void Remove(ResourceRequest Request)
        //{
        //    throw new NotImplementedException();
        //}

        //public void Set(double NewAmount)
        //{
        //    throw new NotImplementedException();
        //}

        ///// <summary>
        ///// 
        ///// </summary>
        //public void Initialise()
        //{
        //    throw new NotImplementedException();
        //}

        ///// <summary>
        ///// Last transaction received
        ///// </summary>
        //[XmlIgnore]
        //public ResourceTransaction LastTransaction { get; set; }

        //public double Amount => throw new NotImplementedException();
    }

    /// <summary>
    /// Individual store of uncollected manure
    /// </summary>
    public class ManureStoreUncollected
	{
		/// <summary>
		/// Name of store (eg yards, paddock name etc)
		/// </summary>
		public string Name { get; set; }

		/// <summary>
		/// Pools of manure in this store
		/// </summary>
		public List<ManurePool> Pools = new List<ManurePool>();
	}

	/// <summary>
	/// Individual uncollected manure pool to track age and decomposition
	/// </summary>
	public class ManurePool
	{
		/// <summary>
		/// Age of pool (in timesteps)
		/// </summary>
		public int Age { get; set; }
		/// <summary>
		/// Amount (dry weight) in pool
		/// </summary>
		public double Amount { get; set; }
        /// <summary>
        /// Proportion water in pool
        /// </summary>
        public double ProportionMoisture { get; set; }

        /// <summary>
        /// Acluclate wet weight of pool
        /// </summary>
        /// <param name="MoistureDecayRate"></param>
        /// <param name="ProportionMoistureFresh"></param>
        /// <returns></returns>
        public double WetWeight(double MoistureDecayRate, double ProportionMoistureFresh)
		{
			double moisture = ProportionMoistureFresh;
			for (int i = 0; i < Age; i++)
			{
				moisture *= MoistureDecayRate;
			}
            moisture = Math.Max(moisture, 0.05);
			return Amount * (1 + moisture);
		}

	}
}
