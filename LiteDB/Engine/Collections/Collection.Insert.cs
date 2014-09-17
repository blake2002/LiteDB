﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace LiteDB
{
    public partial class Collection<T>
    {
        /// <summary>
        /// Insert a object on collection using a key
        /// </summary>
        public virtual void Insert(object id, T document)
        {
            if (id == null) throw new ArgumentNullException("id");
            if (document == null) throw new ArgumentNullException("document");

            // serialize object
            var doc = new BsonDocument(document);
            var bytes = doc.ToBson();

            _engine.Transaction.Begin();

            try
            {
                var col = this.GetCollectionPage();

                // storage in data pages - returns dataBlock address
                var dataBlock = _engine.Data.Insert(col, bytes);

                // store id in a PK index [0 array]
                var pk = _engine.Indexer.AddNode(col.PK, id);

                // do links between index <-> data block
                pk.DataBlock = dataBlock.Position;
                dataBlock.IndexRef[0] = pk.Position;

                // for each index, insert new IndexNode
                for (byte i = 1; i < col.Indexes.Length; i++)
                {
                    var index = col.Indexes[i];

                    if (!index.IsEmpty)
                    {
                        var key = doc.GetFieldValue(index.Field);

                        var node = _engine.Indexer.AddNode(index, key);

                        // point my index to data object
                        node.DataBlock = dataBlock.Position;

                        // point my dataBlock
                        dataBlock.IndexRef[i] = node.Position;
                    }
                }

                _engine.Transaction.Commit();
            }
            catch (Exception ex)
            {
                _engine.Transaction.Rollback();
                throw ex;
            }
        }
    }
}
