﻿//------------------------------------------------------------------------------
// <auto-generated>
//     Этот код создан программой.
//     Исполняемая версия:4.0.30319.42000
//
//     Изменения в этом файле могут привести к неправильной работе и будут потеряны в случае
//     повторной генерации кода.
// </auto-generated>
//------------------------------------------------------------------------------

namespace TransactCQRS.EventStore.Resources {
    using System;
    using System.Reflection;
    
    
    /// <summary>
    ///    Класс ресурса со строгой типизацией для поиска локализованных строк и т. п.
    /// </summary>
    // Этот класс был автоматически создан классом StronglyTypedResourceBuilder
    // с помощью таких средств, как ResGen или Visual Studio.
    // Чтобы добавить или удалить элемент, отредактируйте ResX-файл, а затем повторно запустите программу ResGen
    // с параметром /str или заново постройте ваш проект VS.
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    public class TextResource {
        
        private static global::System.Resources.ResourceManager resourceMan;
        
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        internal TextResource() {
        }
        
        /// <summary>
        ///    Возвращает кэшированный экземпляр ResourceManager, используемый этим классом.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        public static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("TransactCQRS.EventStore.Resources.TextResource", typeof(TextResource).GetTypeInfo().Assembly);
                    resourceMan = temp;
                }
                return resourceMan;
            }
        }
        
        /// <summary>
        ///    Переопределяет свойство CurrentUICulture текущего потока для всех
        ///    обращений к ресурсу с помощью этого класса ресурса со строгой типизацией.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        public static global::System.Globalization.CultureInfo Culture {
            get {
                return resourceCulture;
            }
            set {
                resourceCulture = value;
            }
        }
        
        /// <summary>
        ///    Ищет локализованную строку, аналогичную The end of a block chain isn&apos;t found..
        /// </summary>
        public static string BlockChainEndNotFound {
            get {
                return ResourceManager.GetString("BlockChainEndNotFound", resourceCulture);
            }
        }
        
        /// <summary>
        ///    Ищет локализованную строку, аналогичную Entity haven&apos;t identity in scope of current transaction..
        /// </summary>
        public static string EntityHaventIdentity {
            get {
                return ResourceManager.GetString("EntityHaventIdentity", resourceCulture);
            }
        }
        
        /// <summary>
        ///    Ищет локализованную строку, аналогичную Event method &quot;{0}&quot; should be public..
        /// </summary>
        public static string MethodShouldBePublic {
            get {
                return ResourceManager.GetString("MethodShouldBePublic", resourceCulture);
            }
        }
        
        /// <summary>
        ///    Ищет локализованную строку, аналогичную Event method &quot;{0}&quot; should be virtual..
        /// </summary>
        public static string MethodShouldBeVirtual {
            get {
                return ResourceManager.GetString("MethodShouldBeVirtual", resourceCulture);
            }
        }
        
        /// <summary>
        ///    Ищет локализованную строку, аналогичную Event method &quot;{0}&quot; should return class..
        /// </summary>
        public static string MethodShouldReturnClass {
            get {
                return ResourceManager.GetString("MethodShouldReturnClass", resourceCulture);
            }
        }
        
        /// <summary>
        ///    Ищет локализованную строку, аналогичную Event method &quot;{0}&quot; should return void..
        /// </summary>
        public static string MethodShouldReturnVoid {
            get {
                return ResourceManager.GetString("MethodShouldReturnVoid", resourceCulture);
            }
        }
        
        /// <summary>
        ///    Ищет локализованную строку, аналогичную Only value type parameters supported..
        /// </summary>
        public static string OnlyValueTypeSupported {
            get {
                return ResourceManager.GetString("OnlyValueTypeSupported", resourceCulture);
            }
        }
        
        /// <summary>
        ///    Ищет локализованную строку, аналогичную Transaction compilation failed. {0}.
        /// </summary>
        public static string TransactionCompilationFailed {
            get {
                return ResourceManager.GetString("TransactionCompilationFailed", resourceCulture);
            }
        }
        
        /// <summary>
        ///    Ищет локализованную строку, аналогичную Transaction now is in read-only state..
        /// </summary>
        public static string TransactionReadOnly {
            get {
                return ResourceManager.GetString("TransactionReadOnly", resourceCulture);
            }
        }
        
        /// <summary>
        ///    Ищет локализованную строку, аналогичную Unsupported type of transaction detected..
        /// </summary>
        public static string UnsupportedTransactionType {
            get {
                return ResourceManager.GetString("UnsupportedTransactionType", resourceCulture);
            }
        }
        
        /// <summary>
        ///    Ищет локализованную строку, аналогичную Unsupported type of entity..
        /// </summary>
        public static string UnsupportedTypeOfEntity {
            get {
                return ResourceManager.GetString("UnsupportedTypeOfEntity", resourceCulture);
            }
        }
    }
}
