namespace TcpClientDevice.Protocol;

public interface IPacketTypeResolver
{
	PacketTypeInfo Resolve(string message);
}
