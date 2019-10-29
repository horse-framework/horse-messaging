using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Twino.Core.Http;

namespace Twino.Server.Http
{
    public static class MultipartFormDataReader
    {
        public static List<FormDataItem> Read(string boundary, MemoryStream stream)
        {
            Span<byte> boundarySpan = Encoding.UTF8.GetBytes("--" + boundary);
            List<FormDataItem> items = new List<FormDataItem>();
            int index;
            Span<byte> buffer = stream.GetBuffer();

            do
            {
                index = buffer.IndexOf(boundarySpan);

                if (index < 0)
                    break;

                //reading first boundary, we can validate or skip
                if (index == 0)
                {
                    buffer = buffer.Slice(index + boundarySpan.Length + 2); //+2 CRLF
                    continue;
                }

                //all item data, headers and content included
                Span<byte> itemSpan = buffer.Slice(0, index);
                if (itemSpan.StartsWith(PredefinedHeaders.BOUNDARY_END))
                    break;

                FormDataItem item = CreateItem(itemSpan);

                //single item has data
                if (string.IsNullOrEmpty(item.Boundary))
                    items.Add(item);

                //mixed item, contains multiple items
                else
                {
                    List<FormDataItem> innerItems = Read(item.Boundary, item.Stream);
                    items.AddRange(innerItems);
                }

                buffer = buffer.Slice(index + boundarySpan.Length + 2); //+2 CRLF
            } while (index >= 0);

            //create new form item
            FormDataItem CreateItem(Span<byte> itemSpan)
            {
                FormDataItem item = new FormDataItem();
                bool headersCompleted = false;

                do
                {
                    int lineIndex = itemSpan.IndexOf(HttpReader.CRLF);
                    if (lineIndex < 1)
                        headersCompleted = true;

                    else
                    {
                        Span<byte> line = itemSpan.Slice(0, lineIndex);

                        //content disposition
                        if (line.StartsWith(PredefinedHeaders.CONTENT_DISPOSITION_COLON))
                        {
                            Span<byte> value = line.Slice(PredefinedHeaders.CONTENT_DISPOSITION_COLON.Length);
                            string[] values = Encoding.UTF8.GetString(value).Split(';');
                            item.Type = values[0].Trim();

                            for (int i = 1; i < values.Length; i++)
                            {
                                string val = values[i].Trim();
                                //content disposition name
                                if (val.StartsWith(PredefinedHeaders.NAME_KV_QUOTA))
                                    item.Name = val.Substring(PredefinedHeaders.NAME_KV_QUOTA.Length,
                                                              val.Length - PredefinedHeaders.NAME_KV_QUOTA.Length - 1);

                                //if file, content disposition file name
                                else if (val.StartsWith(PredefinedHeaders.FILENAME_KV_QUOTA))
                                {
                                    item.IsFile = true;
                                    item.Filename = val.Substring(PredefinedHeaders.FILENAME_KV_QUOTA.Length,
                                                                  val.Length - PredefinedHeaders.FILENAME_KV_QUOTA.Length - 1);
                                    
                                }
                            }
                        }

                        //content type
                        else if (line.StartsWith(PredefinedHeaders.CONTENT_TYPE_COLON_BYTES))
                        {
                            Span<byte> value = line.Slice(PredefinedHeaders.CONTENT_TYPE_COLON_BYTES.Length);
                            string contentType = Encoding.UTF8.GetString(value);

                            //multipart mixed data
                            if (contentType.StartsWith(HttpHeaders.MULTIPART_MIXED))
                            {
                                item.ContentType = HttpHeaders.MULTIPART_MIXED;
                                item.Boundary = contentType.Substring(contentType.IndexOf('=') + 1);
                            }

                            //single data
                            else
                                item.ContentType = contentType;
                        }

                        //transfer encoding binary
                        else if (line.StartsWith(PredefinedHeaders.CONTENT_TRANSFER_ENCODING))
                        {
                            Span<byte> value = line.Slice(PredefinedHeaders.CONTENT_TRANSFER_ENCODING.Length);
                            item.TransferEncoding = Encoding.UTF8.GetString(value);
                        }
                    }

                    itemSpan = itemSpan.Slice(lineIndex + 2);
                } while (!headersCompleted);

                item.Stream = new MemoryStream(itemSpan.Slice(0, itemSpan.Length - 2).ToArray());
                return item;
            }

            return items;
        }
    }
}