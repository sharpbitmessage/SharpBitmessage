using System;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Threading;
using bitmessage.network;

namespace bitmessage
{
	public class ProofOfWork
	{
		public const int PayloadLengthExtraBytes = 14000;
		private const int AverageProofOfWorkNonceTrialsPerByte = 320;

		public static UInt64 Target(int length)
		{
			return (UInt64) ((decimal) Math.Pow(2, 64)/((length + PayloadLengthExtraBytes)*AverageProofOfWorkNonceTrialsPerByte));
		}

		public UInt64 Target()
		{
			return
				(UInt64) ((decimal) Math.Pow(2, 64)/((_data.Length + PayloadLengthExtraBytes)*AverageProofOfWorkNonceTrialsPerByte));
		}

		private readonly byte[] _data;
		private ProofOfWork(byte[] data)
		{
			_data = data;
		}

		public static byte[] AddPow(byte[] data)
		{
			return new ProofOfWork(data).AddPow();
		}

		private bool _initialHashFind;
		private readonly EventWaitHandle _ewh = new ManualResetEvent(false);
		private byte[] _currentInitialHash = new byte[8 + 512 / 8];

		static void CalcHash512ThreadProc(object o)
		{
			var proofOfWork = o as ProofOfWork;
			if (proofOfWork == null) return;

			UInt64 target = proofOfWork.Target();
			byte[] initialHash = new byte[8 + 512/8];

			using (var sha512 = new SHA512Managed())
			{
				UInt64 trialValue;
				do
				{
					lock (proofOfWork._currentInitialHash)
					{
						if (proofOfWork._initialHashFind) return;
						for (int i = 0; i < 9; ++i) // nonce = nonce + 1
						{
							proofOfWork._currentInitialHash[i] += 1;
							if (proofOfWork._currentInitialHash[i] != 0) break;
							if (i == 8) throw new Exception("don't can calculate pow");
						}
						Array.Copy(proofOfWork._currentInitialHash, initialHash, initialHash.Length);
					}

					byte[] resultHash = sha512.ComputeHash(sha512.ComputeHash(initialHash));

					int pos = 0;
					trialValue = resultHash.ReadUInt64(ref pos);
				} while (trialValue > target);

				lock(proofOfWork._currentInitialHash)
				{
					proofOfWork._initialHashFind = true;
					proofOfWork._currentInitialHash = initialHash;
					proofOfWork._ewh.Set();
				}
			}
		}

		private byte[] AddPow()
		{
			Debug.WriteLine("Start Calc ProofOfWork");
			using (var sha512 = new SHA512Managed())
				Buffer.BlockCopy(sha512.ComputeHash(_data), 0, _currentInitialHash, 8, 512 / 8);

			for (int i = 0; i < Environment.ProcessorCount; ++i)
				new Thread(CalcHash512ThreadProc) { Name = "Calc ProofOfWork" }.Start(this);

			_ewh.WaitOne();

			byte[] result = new byte[8 + _data.Length];
			Buffer.BlockCopy(_currentInitialHash, 0, result, 0, 8);
			Buffer.BlockCopy(_data, 0, result, 8, _data.Length);

			Debug.WriteLine("End Calc ProofOfWork");
			return result;
		}

	}
}
