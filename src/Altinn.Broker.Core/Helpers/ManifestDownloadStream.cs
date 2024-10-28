using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

using Altinn.Broker.Core.Domain;

namespace Altinn.Broker.Core.Helpers;
public class ManifestDownloadStream : Stream, IManifestDownloadStream
{
    private ReadOnlyMemory<byte> _content;

    private bool _isOpen;

    private int _position;

    public override bool CanRead => _isOpen;

    public override bool CanSeek => _isOpen;

    public override bool CanWrite => false;

    public override long Length
    {
        get
        {
            ValidateNotClosed();
            return _content.Length;
        }
    }

    public override long Position
    {
        get
        {
            ValidateNotClosed();
            return _position;
        }
        set
        {
            ValidateNotClosed();
            if (value < 0 || value > int.MaxValue)
            {
                throw new ArgumentOutOfRangeException("value");
            }

            _position = (int)value;
        }
    }

    public ManifestDownloadStream(ReadOnlyMemory<byte> content)
    {
        _content = content;
        _isOpen = true;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        ValidateNotClosed();
        long num = origin switch
        {
            SeekOrigin.End => _content.Length + offset,
            SeekOrigin.Current => _position + offset,
            SeekOrigin.Begin => offset,
            _ => throw new ArgumentOutOfRangeException("origin"),
        };
        if (num > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException("offset");
        }

        if (num < 0)
        {
            throw new IOException("An attempt was made to move the position before the beginning of the stream.");
        }

        _position = (int)num;
        return _position;
    }

    public override int ReadByte()
    {
        ValidateNotClosed();
        ReadOnlySpan<byte> span = _content.Span;
        if (_position >= span.Length)
        {
            return -1;
        }

        return span[_position++];
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        ValidateNotClosed();
        ValidateReadArrayArguments(buffer, offset, count);
        return ReadBuffer(new Span<byte>(buffer, offset, count));
    }

    private int ReadBuffer(Span<byte> buffer)
    {
        int num = _content.Length - _position;
        if (num <= 0 || buffer.Length == 0)
        {
            return 0;
        }

        ReadOnlySpan<byte> readOnlySpan;
        if (num <= buffer.Length)
        {
            readOnlySpan = _content.Span;
            readOnlySpan = readOnlySpan.Slice(_position);
            readOnlySpan.CopyTo(buffer);
            _position = _content.Length;
            return num;
        }

        readOnlySpan = _content.Span;
        readOnlySpan = readOnlySpan.Slice(_position, buffer.Length);
        readOnlySpan.CopyTo(buffer);
        _position += buffer.Length;
        return buffer.Length;
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        ValidateNotClosed();
        ValidateReadArrayArguments(buffer, offset, count);
        if (!cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(ReadBuffer(new Span<byte>(buffer, offset, count)));
        }

        return Task.FromCanceled<int>(cancellationToken);
    }

    public override void Flush()
    {
    }

    public override Task FlushAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public override void SetLength(long value)
    {
        throw new NotSupportedException();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException();
    }

    private static void ValidateReadArrayArguments(byte[] buffer, int offset, int count)
    {
        if (buffer == null)
        {
            throw new ArgumentNullException("buffer");
        }

        if (offset < 0)
        {
            throw new ArgumentOutOfRangeException("offset");
        }

        if (count < 0 || buffer.Length - offset < count)
        {
            throw new ArgumentOutOfRangeException("count");
        }
    }

    private void ValidateNotClosed()
    {
        if (!_isOpen)
        {
            throw new ObjectDisposedException(null, "Cannot access a closed Stream");
        }
    }

    protected override void Dispose(bool disposing)
    {
        try
        {
            if (disposing)
            {
                _isOpen = false;
                _content = default(ReadOnlyMemory<byte>);
            }
        }
        finally
        {
            base.Dispose(disposing);
        }
    }


    public async Task AddManifestFile(FileTransferEntity fileTransferEntity)
    {
        ValidateNotClosed();

        if (!IsZipFile())
        {
            throw new InvalidOperationException("The stream must contain a valid ZIP archive");
        }

        // Create a memory stream to hold our modified ZIP content
        using var modifiedZipStream = new MemoryStream();

        // Copy current content to the working memory stream
        Position = 0;
        await CopyToAsync(modifiedZipStream);
        modifiedZipStream.Position = 0;

        using (var archive = new ZipArchive(modifiedZipStream, ZipArchiveMode.Update, true))
        {
            // Remove existing manifest files
            var manifestEntry = archive.GetEntry("Manifest.xml");
            manifestEntry?.Delete();

            var recipientsEntry = archive.GetEntry("Recipients.xml");
            recipientsEntry?.Delete();

            // Create new manifest entry
            var newManifestEntry = archive.CreateEntry("Manifest.xml");
            using (var manifestStream = newManifestEntry.Open())
            {
                var manifest = fileTransferEntity.CreateManifest();
                await SerializeManifestAsync(manifest, manifestStream);
            }
        }

        // Update the content of our DownloadStream with the modified ZIP
        _content = new ReadOnlyMemory<byte>(modifiedZipStream.ToArray());
        _position = 0;
    }

    private bool IsZipFile()
    {
        if (_content.Length < 4)
        {
            return false;
        }

        // Check for ZIP magic number
        var span = _content.Span;
        return span[0] == 0x50 && span[1] == 0x4B && span[2] == 0x03 && span[3] == 0x04;
    }
    private async Task SerializeManifestAsync(BrokerServiceManifest manifest, Stream stream)
    {
        var ns = new XmlSerializerNamespaces();
        ns.Add("", "http://schema.altinn.no/services/ServiceEngine/Broker/2015/06");
        ns.Add("xsd", "http://www.w3.org/2001/XMLSchema");
        ns.Add("xsi", "http://www.w3.org/2001/XMLSchema-instance");

        var serializer = new XmlSerializer(typeof(BrokerServiceManifest));
        var xmlWriter = XmlWriter.Create(stream, new XmlWriterSettings { OmitXmlDeclaration = false, Indent = true, Encoding = Encoding.Unicode });
        await Task.Run(() => serializer.Serialize(xmlWriter, manifest, ns));
    }
}
