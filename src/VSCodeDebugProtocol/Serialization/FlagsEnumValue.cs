using System;

namespace Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Serialization
{
	internal class FlagsEnumValue<T> : FlagsEnumValueBase<T> where T : struct, Enum
	{
		private T _value;

		public T Value
		{
			get
			{
				return _value;
			}
			set
			{
				_value = value;
				OnValueChanged();
			}
		}

		public override bool ShouldSerialize => true;

		protected override bool TryGetValue(out T value)
		{
			value = Value;
			return true;
		}

		protected override void SetValue(T value, bool isNull)
		{
			_value = value;
		}
	}
}
