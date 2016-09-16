namespace TransactCQRS.EventStore
{
	public abstract class ChainBlock : AbstractTransaction
	{
		public IReference<ChainBlock> Previous { get; }
		public string CheckSum { get; private set; }

		protected ChainBlock(IReference<ChainBlock> previous)
		{
			Previous = previous;
		}

		[Event]
		public void AddTransaction(AbstractTransaction value)
		{
			
		}

		[Event]
		public void TransactionFailed(AbstractTransaction value)
		{

		}

		[Event]
		public void SetCheckSum(string value)
		{
			CheckSum = value;
		}

		public void BeforeSave()
		{
			SetCheckSum(GenerateCheckSum());
		}

		private string GenerateCheckSum()
		{
			throw new System.NotImplementedException();
		}
	}
}
