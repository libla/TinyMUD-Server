﻿using System;
using System.IO;
using System.Text;
using System.Net;
using System.Net.Security;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace TinyMUD
{
	public static class HttpRequest
	{
		private static readonly string DefaultUserAgent = "Mozilla/4.0 (compatible; MSIE 6.0; Windows NT 5.2; SV1; .NET CLR 1.1.4322; .NET CLR 2.0.50727)";

		public interface Form
		{
			byte[] Value { get; }
			bool HasValue { get; }
		}

		#region 预设Form类
		public class ParamForm : Dictionary<string, string>, Form
		{
			private readonly Encoding encoding;

			public ParamForm() : this(Encoding.UTF8) { }
			public ParamForm(Encoding encoding)
			{
				this.encoding = encoding;
			}

			public ParamForm(IDictionary<string, string> dictionary) : this(dictionary, Encoding.UTF8) { }
			public ParamForm(IDictionary<string, string> dictionary, Encoding encoding) : base(dictionary)
			{
				this.encoding = encoding;
			}

			public byte[] Value
			{
				get
				{
					StringBuilder buffer = new StringBuilder();
					foreach (var kv in this)
					{
						buffer.AppendFormat(buffer.Length == 0 ? "{0}={1}" : "&{0}={1}", kv.Key, kv.Value);
					}
					return encoding.GetBytes(buffer.ToString());
				}
			}

			public bool HasValue
			{
				get { return Count != 0; }
			}
		}

		public class BytesForm : Form
		{
			private readonly byte[] bytes;

			public BytesForm(byte[] bytes)
			{
				this.bytes = bytes;
			}

			public byte[] Value
			{
				get { return bytes; }
			}

			public bool HasValue
			{
				get { return bytes != null && bytes.Length != 0; }
			}
		}
		#endregion

		/// <summary>
		/// 创建GET方式的HTTP请求
		/// </summary>
		/// <param name="url">请求的URL</param>
		/// <param name="timeout">请求的超时时间</param>
		/// <param name="userAgent">请求的客户端浏览器信息，可以为空</param>
		/// <param name="cookies">随同HTTP请求发送的Cookie信息，如果不需要身份验证可以为空</param>
		public static Task<HttpWebResponse> Get(string url, int? timeout = null, string userAgent = null,
												CookieCollection cookies = null)
		{
			if (string.IsNullOrEmpty(url))
				throw new ArgumentNullException("url");

			HttpWebRequest request = WebRequest.Create(url) as HttpWebRequest;
			request.Method = "GET";
			request.UserAgent = !string.IsNullOrEmpty(userAgent) ? userAgent : DefaultUserAgent;
			if (timeout.HasValue)
			{
				request.Timeout = timeout.Value;
			}
			if (cookies != null)
			{
				request.CookieContainer = new CookieContainer();
				request.CookieContainer.Add(cookies);
			}
			return request.GetResponseAsync().ContinueWith(task =>
			{
				return task.Result as HttpWebResponse;
			});
		}

		private static readonly RemoteCertificateValidationCallback CheckValidationResult = (sender, certificate, chain, errors) => true;
		/// <summary>
		/// 创建POST方式的HTTP请求
		/// </summary>
		/// <param name="url">请求的URL</param>
		/// <param name="form">随同请求POST的FORM</param>
		/// <param name="timeout">请求的超时时间</param>
		/// <param name="userAgent">请求的客户端浏览器信息，可以为空</param>
		/// <param name="cookies">随同HTTP请求发送的Cookie信息，如果不需要身份验证可以为空</param>
		public static Task<HttpWebResponse> Post(string url, Form form, int? timeout = null, string userAgent = null,
												CookieCollection cookies = null)
		{
			if (string.IsNullOrEmpty(url))
				throw new ArgumentNullException("url");

			HttpWebRequest request;
			//如果是发送HTTPS请求
			if (url.StartsWith("https", StringComparison.OrdinalIgnoreCase))
			{
				ServicePointManager.ServerCertificateValidationCallback = CheckValidationResult;
				request = WebRequest.Create(url) as HttpWebRequest;
				request.ProtocolVersion = HttpVersion.Version10;
			}
			else
			{
				request = WebRequest.Create(url) as HttpWebRequest;
			}
			request.Method = "POST";
			request.ContentType = "application/x-www-form-urlencoded; charset=UTF-8";
			request.UserAgent = !string.IsNullOrEmpty(userAgent) ? userAgent : DefaultUserAgent;
			if (timeout.HasValue)
			{
				request.Timeout = timeout.Value;
			}
			if (cookies != null)
			{
				request.CookieContainer = new CookieContainer();
				request.CookieContainer.Add(cookies);
			}
			//如果需要POST数据
			if (form != null && form.HasValue)
			{
				byte[] data = form.Value;
				return request.GetRequestStreamAsync().ContinueWith(task1 =>
				{
					Stream stream = task1.Result;
					return stream.WriteAsync(data, 0, data.Length).ContinueWith(task2 =>
					{
						try
						{
							task2.Wait();
						}
						finally
						{
							stream.Dispose();
						}
						return request.GetResponseAsync().ContinueWith(task3 =>
						{
							return task3.Result as HttpWebResponse;
						});
					}).Unwrap();
				}).Unwrap();
			}
			return request.GetResponseAsync().ContinueWith(task =>
			{
				return task.Result as HttpWebResponse;
			});
		}
	}
}