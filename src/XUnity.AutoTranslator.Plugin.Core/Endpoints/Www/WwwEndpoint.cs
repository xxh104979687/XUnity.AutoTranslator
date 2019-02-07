﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Reflection;
using System.Text;
using Harmony;
using UnityEngine;
using XUnity.AutoTranslator.Plugin.Core.Configuration;
using XUnity.AutoTranslator.Plugin.Core.Web;

namespace XUnity.AutoTranslator.Plugin.Core.Endpoints.Www
{
   /// <summary>
   /// An implementation of ITranslateEndpoint that simplifies implementing
   /// the interface based on a web service.
   ///
   /// Consider using HttpEndpoint instead!
   /// </summary>
   public abstract class WwwEndpoint : ITranslateEndpoint
   {
      private static readonly ConstructorInfo WwwConstructor = Constants.ClrTypes.WWW.GetConstructor( new[] { typeof( string ), typeof( byte[] ), typeof( Dictionary<string, string> ) } );

      /// <summary>
      /// Gets the id of the ITranslateEndpoint that is used as a configuration parameter.
      /// </summary>
      public abstract string Id { get; }

      /// <summary>
      /// Gets a friendly name that can be displayed to the user representing the plugin.
      /// </summary>
      public abstract string FriendlyName { get; }

      /// <summary>
      /// Gets the maximum concurrency for the endpoint. This specifies how many times "Translate"
      /// can be called before it returns.
      /// </summary>
      public int MaxConcurrency => 1;

      /// <summary>
      /// Callback that can be overwritten that is called before any requests are sent out.
      /// </summary>
      /// <param name="context"></param>
      /// <returns></returns>
      public virtual IEnumerator OnBeforeTranslate( IWwwTranslationContext context ) => null;

      /// <summary>
      /// Called during initialization. Use this to initialize plugin or throw exception if impossible.
      /// </summary>
      public abstract void Initialize( IInitializationContext context );

      /// <summary>
      /// Callback that must be overwritten to create the request object.
      /// </summary>
      /// <param name="context"></param>
      public abstract void OnCreateRequest( IWwwRequestCreationContext context );

      /// <summary>
      /// Callback that must overwritten to extract the text from the web response.
      /// </summary>
      /// <param name="context"></param>
      public abstract void OnExtractTranslation( IWwwTranslationExtractionContext context );

      /// <summary>
      /// Creates a WWW object used to send a web request.
      ///
      /// Consider using this instead of default WWW constructor, as that
      /// may cause issues with different unity versions.
      /// </summary>
      /// <param name="address"></param>
      /// <param name="data"></param>
      /// <param name="headers"></param>
      /// <returns></returns>
      protected WWW CreateWww( string address, byte[] data, Dictionary<string, string> headers )
      {
         return (WWW)WwwConstructor.Invoke( new object[] { address, data, headers } );
      }

      /// <summary>
      /// Attempt to translated the provided untranslated text. Will be used in a "coroutine",
      /// so it can be implemented in an asynchronous fashion.
      /// </summary>
      public IEnumerator Translate( ITranslationContext context )
      {
         var wwwContext = new WwwTranslationContext( context );

         // allow implementer of HttpEndpoint to do anything before starting translation
         var setup = OnBeforeTranslate( wwwContext );
         if( setup != null )
         {
            while( setup.MoveNext() ) yield return setup.Current; 
         }

         // prepare request
         OnCreateRequest( wwwContext );
         if( wwwContext.RequestInfo == null ) wwwContext.Fail( "No request object was provided by the translator.", null );

         var request = wwwContext.RequestInfo;
         var url = request.Address;
         var data = request.Data;
         var headers = request.Headers;

         // execute request
         var www = CreateWww( request.Address, data != null ? Encoding.UTF8.GetBytes( data ) : null, headers );

         // wait for completion
         yield return www;

         // extract error
         string error = (string)AccessTools.Property( Constants.ClrTypes.WWW, "error" ).GetValue( www, null );
         if( error != null ) wwwContext.Fail( "Error occurred while retrieving translation. " + error, null );

         // extract text
         var text = (string)AccessTools.Property( Constants.ClrTypes.WWW, "text" ).GetValue( www, null );
         if( text == null ) wwwContext.Fail( "Error occurred while extracting text from response.", null ); 

         wwwContext.ResponseData = text;

         // extract text
         OnExtractTranslation( wwwContext );
      }
   }
}
